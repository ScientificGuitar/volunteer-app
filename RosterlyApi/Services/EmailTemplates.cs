using System.Web;

namespace RosterlyApi.Services;

public static class EmailTemplates
{
    public static (string Subject, string HtmlBody, string TextBody) BuildSignupConfirmation(
        string volunteerName,
        string organizationName,
        string eventTitle,
        DateOnly eventDate,
        TimeOnly startTime,
        TimeOnly endTime,
        string manageUrl,
        string? location = null,
        CalendarInviteBuilder.CalendarLinks? calendarLinks = null,
        bool hasCalendarAttachment = false)
    {
        var subject = $"Confirm your signup: {eventTitle}";
        var encodedLocation = string.IsNullOrWhiteSpace(location)
            ? null
            : HttpUtility.HtmlEncode(location.Trim());

        var locationHtml = encodedLocation is null
            ? ""
            : $"""
                <p style="margin:8px 0 0;font-size:14px;color:#3f3f46;">📍 {encodedLocation}</p>
                """;

        var calendarHtml = calendarLinks is null
            ? ""
            : $"""
                <tr>
                  <td style="padding:0 32px 8px;">
                    <p style="margin:0 0 8px;font-size:14px;color:#3f3f46;">Add it to your calendar:</p>
                    <p style="margin:0;font-size:14px;">
                      <a href="{HttpUtility.HtmlEncode(calendarLinks.Google)}" style="color:#18181b;">Google</a>
                      <span style="color:#a1a1aa;">&nbsp;·&nbsp;</span>
                      <a href="{HttpUtility.HtmlEncode(calendarLinks.Outlook)}" style="color:#18181b;">Outlook</a>
                      <span style="color:#a1a1aa;">&nbsp;·&nbsp;</span>
                      <a href="{HttpUtility.HtmlEncode(calendarLinks.Yahoo)}" style="color:#18181b;">Yahoo</a>
                    </p>
                    {(hasCalendarAttachment ? """<p style="margin:8px 0 0;font-size:13px;color:#71717a;">An .ics invite is also attached to this email.</p>""" : "")}
                  </td>
                </tr>
                """;

        var html = $"""
            <!DOCTYPE html>
            <html>
            <body style="margin:0;padding:0;background-color:#f4f4f5;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;">
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background-color:#f4f4f5;padding:32px 16px;">
                <tr>
                  <td align="center">
                    <table role="presentation" width="100%" maxwidth="480" cellpadding="0" cellspacing="0" style="max-width:480px;width:100%;background-color:#ffffff;border-radius:12px;overflow:hidden;">
                      <tr>
                        <td style="padding:32px 32px 24px;">
                          <p style="margin:0 0 4px;font-size:14px;color:#71717a;">{HttpUtility.HtmlEncode(organizationName)}</p>
                          <h1 style="margin:0 0 4px;font-size:22px;color:#18181b;">Confirm your signup</h1>
                          <h2 style="margin:0 0 16px;font-size:16px;font-weight:500;color:#3f3f46;">{HttpUtility.HtmlEncode(eventTitle)}</h2>
                          <p style="margin:0;font-size:14px;color:#3f3f46;">You've signed up as a volunteer for the shift below. Your spot isn't confirmed until you click the button.</p>
                        </td>
                      </tr>
                      <tr>
                        <td style="padding:0 32px 8px;">
                          <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background-color:#fafafa;border-radius:8px;">
                            <tr>
                              <td style="padding:16px;">
                                <p style="margin:0 0 4px;font-size:14px;color:#18181b;"><strong>{eventDate:dddd d MMMM yyyy}</strong></p>
                                <p style="margin:0;font-size:14px;color:#3f3f46;">{startTime:HH:mm}&ndash;{endTime:HH:mm}</p>
                                {locationHtml}
                                <p style="margin:12px 0 0;font-size:14px;color:#3f3f46;">Name: <strong>{HttpUtility.HtmlEncode(volunteerName)}</strong></p>
                              </td>
                            </tr>
                          </table>
                        </td>
                      </tr>
                      {calendarHtml}
                      <tr>
                        <td style="padding:24px 32px 32px;">
                          <a href="{HttpUtility.HtmlEncode(manageUrl)}" style="display:inline-block;background-color:#18181b;color:#ffffff;text-decoration:none;font-size:14px;font-weight:600;padding:12px 24px;border-radius:8px;">Confirm my signup</a>
                          <p style="margin:16px 0 0;font-size:13px;color:#71717a;">
                            Clicking the button confirms your spot. Afterwards you can use this link anytime to view or cancel your signup. If you didn't sign up, you can ignore this email.
                          </p>
                        </td>
                      </tr>
                    </table>
                  </td>
                </tr>
              </table>
            </body>
            </html>
            """;

        var textLocation = string.IsNullOrWhiteSpace(location) ? "" : $"\nLocation: {location.Trim()}";
        var textCalendar = calendarLinks is null ? "" : $"""

            Add it to your calendar:
            Google: {calendarLinks.Google}
            Outlook: {calendarLinks.Outlook}
            Yahoo: {calendarLinks.Yahoo}
            """ + (hasCalendarAttachment ? "\nAn .ics invite is also attached to this email." : "");

        var text = $"""
            {organizationName}

            Confirm your signup for: {eventTitle}
            {eventDate:dddd d MMMM yyyy}, {startTime:HH:mm}–{endTime:HH:mm}{textLocation}
            Name: {volunteerName}

            Your spot isn't confirmed until you click the link below:

            {manageUrl}{textCalendar}

            Afterwards, you can use this link anytime to view or cancel your signup.
            """;

        return (subject, html, text);
    }
}