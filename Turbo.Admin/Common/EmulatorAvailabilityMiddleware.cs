using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Turbo.Admin.Common;

public static class EmulatorAvailabilityMiddleware
{
    public static IApplicationBuilder UseEmulatorAvailabilityHandling(
        this IApplicationBuilder app
    ) =>
        app.Use(
            async (context, next) =>
            {
                var connectionState = context.RequestServices.GetRequiredService<
                    OrleansConnectionState
                >();

                try
                {
                    await next(context).ConfigureAwait(false);
                }
                catch (Exception ex) when (!connectionState.IsConnected)
                {
                    var logger = context
                        .RequestServices.GetRequiredService<ILoggerFactory>()
                        .CreateLogger("EmulatorAvailability");

                    logger.LogWarning(
                        ex,
                        "Grain call failed for {Path}; emulator is not running.",
                        context.Request.Path
                    );

                    context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                    await context
                        .Response.WriteAsJsonAsync(new { error = "Emulator is not running." })
                        .ConfigureAwait(false);
                }
            }
        );
}
