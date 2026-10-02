using System.Diagnostics;
using OpenTelemetry;

namespace Turbo.Main.Extensions;

/// <summary>Removes free-form exception text from exported activities.</summary>
public sealed class TelemetryPrivacyProcessor : BaseProcessor<Activity>
{
    public override void OnEnd(Activity activity)
    {
        activity.SetTag("exception.message", null);
        activity.SetTag("exception.stacktrace", null);
        activity.SetTag("exception.stack_trace", null);
        activity.SetTag("error.message", null);

        if (activity.Status == ActivityStatusCode.Error)
        {
            activity.SetStatus(ActivityStatusCode.Error);
        }
    }
}
