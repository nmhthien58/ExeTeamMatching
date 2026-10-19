using BuildingBlocks.Messaging;
using BuildingBlocks.Observability;

namespace BuildingBlocks.Tests;

public sealed class MessagingTests
{
    private sealed record InvitationSent(Guid InvitationId, string StudentCode);

    [Fact]
    public void Envelope_round_trips_with_camel_case_fields()
    {
        var payload = new InvitationSent(Guid.NewGuid(), "SE184118");
        var envelope = EventEnvelope.Create("InvitationSent", payload, "team", "corr-1");

        var json = envelope.Serialize();
        var back = EventEnvelope.Deserialize(json);

        Assert.Contains("\"eventId\"", json);
        Assert.Contains("\"studentCode\":\"SE184118\"", json);
        Assert.Equal(envelope.EventId, back.EventId);
        Assert.Equal("team", back.Producer);
        Assert.Equal("corr-1", back.CorrelationId);
        Assert.Equal(payload, back.GetPayload<InvitationSent>());
    }

    [Fact]
    public void Dead_letter_topic_has_dlq_suffix()
    {
        Assert.Equal("exe.team.dlq", Topics.DeadLetter(Topics.Team));
    }

    [Fact]
    public async Task Correlation_scope_restores_previous_value()
    {
        using (CorrelationContext.Begin("outer"))
        {
            using (CorrelationContext.Begin("inner"))
            {
                Assert.Equal("inner", CorrelationContext.Id);
            }
            Assert.Equal("outer", CorrelationContext.Id);

            // AsyncLocal đi theo luồng async.
            await Task.Yield();
            Assert.Equal("outer", CorrelationContext.Id);
        }
    }

    [Fact]
    public async Task Correlation_handler_adds_header_to_outgoing_requests()
    {
        var inner = new CaptureHandler();
        var client = new HttpClient(new CorrelationIdHandler { InnerHandler = inner });

        using (CorrelationContext.Begin("xyz"))
        {
            await client.GetAsync("http://example.test/");
        }

        Assert.Equal("xyz", inner.Last!.Headers.GetValues(CorrelationContext.HeaderName).Single());
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Last { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Last = request;
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        }
    }
}
