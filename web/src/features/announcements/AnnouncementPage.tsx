import { useState, type FormEvent } from 'react'
import { ApiError } from '@/shared/api/apiClient'
import { Banner, Button, Icon } from '@/shared/components/ui'
import { PageHeader } from '@/shared/layout/PageHeader'
import {
  AUDIENCE_OPTIONS,
  COURSE_OPTIONS,
  useSendAnnouncement,
  type AnnouncementAudience,
  type AnnouncementResponse,
} from './api'

const fieldClass =
  'h-10 rounded-sm border border-border-strong bg-surface px-3 text-body focus:border-primary focus:outline-none'
const textareaClass =
  'min-h-36 rounded-sm border border-border-strong bg-surface p-3 text-body focus:border-primary focus:outline-none resize-y'

/**
 * W23 – Gửi thông báo chung (FE-29).
 * Admin soạn thông báo cho sinh viên / giảng viên / mentor (in-app + email).
 */
export function AnnouncementPage() {
  const [audience, setAudience] = useState<AnnouncementAudience>('EVERYONE')
  const [courseCode, setCourseCode] = useState<'EXE101' | 'EXE201' | ''>('')
  const [title, setTitle] = useState('')
  const [content, setContent] = useState('')
  const [sendEmail, setSendEmail] = useState(true)
  const [result, setResult] = useState<AnnouncementResponse | null>(null)
  const [errorMessage, setErrorMessage] = useState<string | null>(null)

  const sendMutation = useSendAnnouncement()

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault()
    setErrorMessage(null)

    if (!title.trim()) {
      setErrorMessage('Vui lòng nhập tiêu đề thông báo.')
      return
    }
    if (!content.trim()) {
      setErrorMessage('Vui lòng nhập nội dung thông báo.')
      return
    }

    try {
      const response = await sendMutation.mutateAsync({
        audience,
        courseCode: courseCode ? courseCode : null,
        title: title.trim(),
        content: content.trim(),
        sendEmail,
      })
      setResult(response)
    } catch (err) {
      if (err instanceof ApiError) {
        setErrorMessage(err.title || 'Không thể gửi thông báo. Vui lòng thử lại.')
      } else {
        setErrorMessage('Đã xảy ra lỗi khi gửi thông báo.')
      }
    }
  }

  const handleReset = () => {
    setAudience('EVERYONE')
    setCourseCode('')
    setTitle('')
    setContent('')
    setSendEmail(true)
    setResult(null)
    setErrorMessage(null)
  }

  return (
    <>
      <PageHeader title="Gửi thông báo chung" />
      <div className="max-w-2xl">
        {result ? (
          <div className="flex flex-col items-center gap-4 rounded-md border border-border bg-surface p-8 text-center shadow-xs">
            <div className="flex size-14 items-center justify-center rounded-full bg-success-container text-success">
              <Icon name="check_circle" size={48} />
            </div>
            <div className="flex flex-col gap-1">
              <h2 className="text-subtitle font-semibold">Đã tiếp nhận thông báo</h2>
              <p className="text-body text-text-secondary">
                Thông báo đang được gửi đến{' '}
                <span className="font-semibold text-text-primary">{result.recipientCount}</span> người nhận qua hệ
                thống{sendEmail ? ' và email' : ''}.
              </p>
            </div>
            <Button variant="outline" onClick={handleReset} className="mt-2">
              Soạn thông báo khác
            </Button>
          </div>
        ) : (
          <form onSubmit={handleSubmit} className="flex flex-col gap-5 rounded-md border border-border bg-surface p-6 shadow-xs">
            {errorMessage && (
              <Banner
                tone="error"
                title={errorMessage}
                action={<Button variant="text" onClick={() => setErrorMessage(null)}>Đóng</Button>}
              />
            )}

            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
              <label className="flex flex-col gap-1.5">
                <span className="text-caption font-medium text-text-secondary">Đối tượng nhận *</span>
                <select
                  value={audience}
                  onChange={(e) => setAudience(e.target.value as AnnouncementAudience)}
                  className={fieldClass}
                  aria-label="Đối tượng nhận"
                >
                  {AUDIENCE_OPTIONS.map((opt) => (
                    <option key={opt.value} value={opt.value}>
                      {opt.label}
                    </option>
                  ))}
                </select>
              </label>

              <label className="flex flex-col gap-1.5">
                <span className="text-caption font-medium text-text-secondary">Khóa học / môn</span>
                <select
                  value={courseCode}
                  onChange={(e) => setCourseCode(e.target.value as 'EXE101' | 'EXE201' | '')}
                  className={fieldClass}
                  aria-label="Khóa học / môn"
                >
                  {COURSE_OPTIONS.map((opt) => (
                    <option key={opt.value} value={opt.value}>
                      {opt.label}
                    </option>
                  ))}
                </select>
              </label>
            </div>

            <label className="flex flex-col gap-1.5">
              <div className="flex justify-between">
                <span className="text-caption font-medium text-text-secondary">Tiêu đề *</span>
                <span className="text-caption text-text-secondary">{title.length}/200</span>
              </div>
              <input
                type="text"
                value={title}
                maxLength={200}
                onChange={(e) => setTitle(e.target.value)}
                placeholder="Nhập tiêu đề thông báo..."
                className={fieldClass}
                aria-label="Tiêu đề"
              />
            </label>

            <label className="flex flex-col gap-1.5">
              <div className="flex justify-between">
                <span className="text-caption font-medium text-text-secondary">Nội dung *</span>
                <span className="text-caption text-text-secondary">{content.length}/2000</span>
              </div>
              <textarea
                value={content}
                maxLength={2000}
                onChange={(e) => setContent(e.target.value)}
                placeholder="Nhập nội dung chi tiết thông báo..."
                className={textareaClass}
                aria-label="Nội dung"
              />
            </label>

            <label className="flex cursor-pointer items-center gap-2">
              <input
                type="checkbox"
                checked={sendEmail}
                onChange={(e) => setSendEmail(e.target.checked)}
                className="size-4 rounded-xs border-border-strong text-primary focus:ring-primary"
              />
              <span className="text-body text-text-primary">Đồng thời gửi email thông báo cho người nhận</span>
            </label>

            <div className="flex justify-end gap-3 pt-2">
              <Button type="button" variant="outline" onClick={handleReset} disabled={sendMutation.isPending}>
                Hủy
              </Button>
              <Button type="submit" variant="primary" icon="send" loading={sendMutation.isPending}>
                Gửi thông báo
              </Button>
            </div>
          </form>
        )}
      </div>
    </>
  )
}
