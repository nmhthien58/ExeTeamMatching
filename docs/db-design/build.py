"""Sinh docs/EXE_Database_Design.pdf từ DB script (BE-60).

Cách chạy (cần Docker và Chrome / Edge):
    python docs/db-design/build.py

Script dựng một container PostgreSQL tạm từ deploy/db/, đọc cấu trúc thật của từng database
(introspect.sql), ghép với chú thích trong file .sql và phần viết tay ở content.py, rồi in ra PDF bằng
Chrome headless. Sửa DB script xong thì chạy lại để PDF luôn khớp với script.
"""

from __future__ import annotations

import html
import json
import os
import re
import shutil
import subprocess
import sys
import time
from datetime import date
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
import content  # noqa: E402

ROOT = Path(__file__).resolve().parents[2]
DB_DIR = ROOT / "deploy" / "db"
HERE = Path(__file__).resolve().parent
OUT_PDF = ROOT / "docs" / "EXE_Database_Design.pdf"
CONTAINER = "exe-dbdoc"

# (database, schema, file script, tiêu đề mục)
DATABASES = [
    ("identity_db", "identity", "01_identity_db.sql", "Identity Service"),
    ("academic_db", "academic", "02_academic_db.sql", "Academic Service"),
    ("team_db", "team", "03_team_db.sql", "Team Service"),
    ("notification_db", "notification", "04_notification_db.sql", "Notification Service"),
    ("collab_db", "collab", "06_collab_db.sql", "Collaboration Service"),
]


# ---------------------------------------------------------------- PostgreSQL tạm

def run(cmd: list[str], **kw) -> subprocess.CompletedProcess:
    env = {**os.environ, "MSYS_NO_PATHCONV": "1"}
    return subprocess.run(cmd, check=True, capture_output=True, text=True, encoding="utf-8", env=env, **kw)


def start_postgres() -> None:
    subprocess.run(["docker", "rm", "-f", CONTAINER], capture_output=True)
    run([
        "docker", "run", "-d", "--name", CONTAINER,
        "-e", "POSTGRES_PASSWORD=dbdoc", "-e", "ADMIN_EMAIL=admin@example.com",
        "-v", f"{DB_DIR}:/docker-entrypoint-initdb.d:ro",
        "-v", f"{HERE}:/dbdoc:ro",
        "postgres:16-alpine",
    ])
    for _ in range(90):
        logs = subprocess.run(["docker", "logs", CONTAINER], capture_output=True, text=True, encoding="utf-8")
        text = logs.stdout + logs.stderr
        if "PostgreSQL init process complete" in text and "ready to accept connections" in text.split("init process complete")[-1]:
            return
        if "ERROR:" in text:
            raise SystemExit("DB script lỗi:\n" + "\n".join(l for l in text.splitlines() if "ERROR" in l))
        time.sleep(1)
    raise SystemExit("PostgreSQL tạm không khởi động kịp.")


def introspect(database: str, schema: str) -> dict:
    out = run(["docker", "exec", CONTAINER, "psql", "-U", "postgres", "-d", database,
               "-v", f"schema={schema}", "-At", "-f", "/dbdoc/introspect.sql"]).stdout
    return json.loads(out)


# ---------------------------------------------------------------- chú thích trong file .sql

SECTION_RE = re.compile(r"^--\s*-{3,}\s*(.+?)\s*-{3,}\s*$")
CREATE_RE = re.compile(r"^CREATE TABLE\s+(\w+)\.(\w+)\s*\(", re.I)
COLUMN_RE = re.compile(r"^\s{2,}(\w+)\s+[^-]*?--\s*(.+)$")


def parse_sql_notes(path: Path) -> tuple[dict, dict, dict]:
    """Trả về (mô tả bảng, ghi chú cột, nhóm/section của bảng) lấy từ comment trong script."""
    table_desc: dict[str, str] = {}
    column_note: dict[tuple[str, str], str] = {}
    table_section: dict[str, str] = {}
    section = "Khác"
    pending: list[str] = []
    current: str | None = None
    for raw in path.read_text(encoding="utf-8").splitlines():
        line = raw.rstrip()
        if current:
            if line.startswith(")"):
                current = None
                continue
            m = COLUMN_RE.match(line)
            if m and not m.group(1).upper().startswith("CONSTRAINT"):
                column_note[(current, m.group(1))] = m.group(2).strip()
            continue
        if m := SECTION_RE.match(line):
            section = m.group(1)
            pending = []
            continue
        if line.startswith("--"):
            pending.append(line[2:].strip())
            continue
        if m := CREATE_RE.match(line):
            current = m.group(2)
            table_desc[current] = " ".join(pending).strip()
            table_section[current] = section
        pending = []
    return table_desc, column_note, table_section


# ---------------------------------------------------------------- HTML

def esc(s: object) -> str:
    return html.escape("" if s is None else str(s))


def short_type(t: str) -> str:
    return (t.replace("character varying", "varchar").replace("timestamp with time zone", "timestamptz")
             .replace("timestamp without time zone", "timestamp").replace("time without time zone", "time")
             .replace("integer[]", "int[]"))


def short_default(d: str | None) -> str:
    if not d:
        return ""
    d = re.sub(r"::[\w ]+(\[\])?", "", d)
    return d.replace("'", "") if len(d) < 40 else d[:37] + "…"


def pretty_sql(d: str) -> str:
    """Bỏ phần ép kiểu PostgreSQL tự thêm để định nghĩa ràng buộc dễ đọc."""
    d = re.sub(r"::(character varying|text|citext|integer|smallint|bpchar|character)(\[\])?", "", d)
    d = re.sub(r"= ANY \(\(?ARRAY\[([^\]]*)\]\)?\)", r"IN (\1)", d)
    d = re.sub(r"\(([\w.]+)\)(?=\s*(IN|~|=|<>|>=|<=|>|<|IS)\b)", r"\1", d)
    for _ in range(3):
        d = re.sub(r"\(\(([^()]*)\)\)", r"(\1)", d)
    return d


def keys_of(table: dict) -> dict[str, list[str]]:
    keys: dict[str, list[str]] = {}
    for k in table["constraints"]:
        tag = {"p": "PK", "f": "FK", "u": "UQ"}.get(k["type"])
        if tag:
            for c in k["cols"] or []:
                if tag not in keys.setdefault(c, []):
                    keys[c].append(tag)
    return keys


def render_table(schema: str, t: dict, desc: str, notes: dict) -> str:
    keys = keys_of(t)
    rows = []
    for c in t["columns"]:
        default = "IDENTITY" if c["identity"] else short_default(c["default"])
        rows.append(
            f"<tr><td class=mono>{esc(c['name'])}</td><td class=mono>{esc(short_type(c['type']))}</td>"
            f"<td class=c>{'✓' if c['nullable'] else ''}</td><td class=c>{' '.join(keys.get(c['name'], []))}</td>"
            f"<td class=mono>{esc(default)}</td><td>{esc(notes.get((t['name'], c['name']), ''))}</td></tr>")
    cons = []
    label = {"p": "PK", "u": "UNIQUE", "f": "FK", "c": "CHECK", "x": "EXCLUDE", "t": "TRIGGER"}
    for k in t["constraints"]:
        cons.append(f"<li><b>{label.get(k['type'], k['type'])}</b> <span class=mono>{esc(k['name'])}</span> "
                    f"<span class=mono dim>{esc(pretty_sql(k['def']))}</span></li>")
    for i in t["indexes"]:
        name = re.search(r"INDEX (\w+)", i).group(1)
        body = i.split(" USING ", 1)[-1]
        kind = "UNIQUE INDEX" if i.startswith("CREATE UNIQUE") else "INDEX"
        cons.append(f"<li><b>{kind}</b> <span class=mono>{esc(name)}</span> <span class=mono dim>{esc(pretty_sql(body))}</span></li>")
    for g in t["triggers"]:
        body = re.sub(r"^CREATE (CONSTRAINT )?TRIGGER \w+ ", "", g["def"])
        cons.append(f"<li><b>TRIGGER</b> <span class=mono>{esc(g['name'])}</span> <span class=mono dim>{esc(body)}</span></li>")
    return (
        f"<section class=tbl><h4><span class=mono>{schema}.{esc(t['name'])}</span></h4>"
        + (f"<p class=desc>{esc(desc)}</p>" if desc else "")
        + "<table class=cols><thead><tr><th>Cột</th><th>Kiểu</th><th>Null</th><th>Khóa</th><th>Mặc định</th><th>Ghi chú</th></tr></thead>"
        + f"<tbody>{''.join(rows)}</tbody></table>"
        + (f"<ul class=cons>{''.join(cons)}</ul>" if cons else "")
        + "</section>")


def render_erd(schema: str, tables: list[dict]) -> str:
    """Sơ đồ Mermaid rút gọn: chỉ cột khóa, quan hệ theo FK trong cùng database."""
    names = {t["name"] for t in tables}
    lines = ["erDiagram"]
    for t in tables:
        keys = keys_of(t)
        cols = [c for c in t["columns"] if keys.get(c["name"])][:8]
        body = "".join(
            f'    {short_type(c["type"]).split("(")[0].replace(" ", "_").replace("[]", "_arr")} {c["name"]} '
            f'{", ".join(k for k in keys[c["name"]] if k in ("PK", "FK")) or "UK"}\n'
            for c in cols)
        lines.append(f"  {t['name']} {{\n{body}  }}")
    seen = set()
    for t in tables:
        for k in t["constraints"]:
            if k["type"] == "f" and k["ref"] in names and (t["name"], k["ref"]) not in seen:
                seen.add((t["name"], k["ref"]))
                nullable = any(c["nullable"] for c in t["columns"] if c["name"] in (k["cols"] or []))
                lines.append(f'  {k["ref"]} ||--{"o" if nullable else "|"}{{ {t["name"]} : "{k["cols"][0]}"')
    return "<pre class=mermaid>\n" + "\n".join(lines) + "\n</pre>"


def build_html() -> str:
    parts: list[str] = []
    totals = []
    for number, (database, schema, script, title) in enumerate(DATABASES, start=4):
        data = introspect(database, schema)
        desc, notes, sections = parse_sql_notes(DB_DIR / script)
        tables = data["tables"]
        totals.append((database, schema, [t["name"] for t in tables]))
        parts.append(f"<h2 class=break>{number}. Schema {schema} — {esc(title)}</h2>"
                     f"<p class=meta>Database <span class=mono>{database}</span>, schema <span class=mono>{schema}</span>, "
                     f"login role <span class=mono>{schema}_svc</span> · {len(tables)} bảng · script "
                     f"<span class=mono>deploy/db/{script}</span></p>")
        parts.append(content.SCHEMA_INTRO.get(schema, ""))
        groups: dict[str, list[dict]] = {}
        for t in tables:
            groups.setdefault(sections.get(t["name"], "Khác"), []).append(t)
        for group, items in groups.items():
            if len(groups) > 1:
                parts.append(f"<h3>{esc(group)}</h3>")
            if len(items) >= 3:
                parts.append(f"<div class=erd>{render_erd(schema, items)}</div>")
            parts.extend(render_table(schema, t, desc.get(t["name"], ""), notes) for t in items)

    overview = content.overview(totals)
    body = (content.cover(date.today()) + overview + content.CONVENTIONS + content.RULES
            + "".join(parts) + content.JOB_AND_MATCHING + content.CROSS_SERVICE + content.CHANGES)
    css = (HERE / "style.css").read_text(encoding="utf-8")
    return f"""<!doctype html><html lang=vi><head><meta charset=utf-8>
<title>EXE Team Matching – Thiết kế Database</title><style>{css}</style></head><body>{body}
<script src="https://cdn.jsdelivr.net/npm/mermaid@10.9.1/dist/mermaid.min.js"></script>
<script>
mermaid.initialize({{ startOnLoad: false, theme: 'neutral', er: {{ useMaxWidth: false, fontSize: 12 }} }});
mermaid.run().then(() => document.body.setAttribute('data-ready', '1'));
</script></body></html>"""


# ---------------------------------------------------------------- PDF

def find_chrome() -> str:
    for p in [
        r"C:\Program Files\Google\Chrome\Application\chrome.exe",
        r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
        "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome",
    ]:
        if Path(p).exists():
            return p
    for name in ("google-chrome", "chromium", "chrome", "msedge"):
        if found := shutil.which(name):
            return found
    raise SystemExit("Không tìm thấy Chrome / Edge để in PDF.")


def main() -> None:
    start_postgres()
    try:
        page = HERE / "build" / "db-design.html"
        page.parent.mkdir(exist_ok=True)
        page.write_text(build_html(), encoding="utf-8")
    finally:
        subprocess.run(["docker", "rm", "-f", CONTAINER], capture_output=True)
    subprocess.run([
        find_chrome(), "--headless=new", "--disable-gpu", "--no-pdf-header-footer",
        "--virtual-time-budget=30000", "--run-all-compositor-stages-before-draw",
        f"--print-to-pdf={OUT_PDF}", page.as_uri(),
    ], check=True, capture_output=True)
    print(f"Đã ghi {OUT_PDF.relative_to(ROOT)} ({OUT_PDF.stat().st_size // 1024} KB) từ {page.relative_to(ROOT)}")


if __name__ == "__main__":
    main()
