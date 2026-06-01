using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.HttpOverrides;
using Resend;

namespace Protoscend.Api
{
    public static class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddOpenApi();
            builder.Services.AddResend(options =>
            {
                options.ApiToken = builder.Configuration["Resend:ApiKey"]
                    ?? throw new Exception("Resend:ApiKey not configured");
            });

            builder.Services.AddCors(options =>
            {
                options.AddDefaultPolicy(policy =>
                {
                    policy.WithOrigins(
                        "https://protoscend.com",
                        "https://www.protoscend.com",
                        "http://localhost:5000",
                        "https://localhost:7049"
                    )
                    .AllowAnyHeader()
                    .AllowAnyMethod();
                });
            });

            var app = builder.Build();

            app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
                ForwardedHeaders =
                    ForwardedHeaders.XForwardedFor |
                    ForwardedHeaders.XForwardedProto
            });

            app.UseRouting();
            app.UseCors();

            app.MapPost("/api/contact", async (ContactRequest model, IConfiguration config, IResend resend) =>
            {
                var errors = new List<ValidationResult>();
                if (!Validator.TryValidateObject(model, new ValidationContext(model), errors, true))
                    return Results.BadRequest(errors.Select(e => e.ErrorMessage));

                var toAddr = config["Email:To"] ?? throw new Exception("Email:To not configured");

                try
                {
                    // 1 — Internal notification
                    await resend.EmailSendAsync(new EmailMessage
                    {
                        From = "PROTOSCEND Website <onboarding@resend.dev>",
                        To = [toAddr],
                        ReplyTo = [$"{model.FirstName} {model.LastName} <{model.Email}>"],
                        Subject = $"[PROTOSCEND] New Enquiry — {model.FirstName} {model.LastName}",
                        HtmlBody = BuildInternalHtml(model)
                    });

                    // 2 — Auto-reply to sender
                    await resend.EmailSendAsync(new EmailMessage
                    {
                        From = "PROTOSCEND Website <onboarding@resend.dev>",
                        To = [model.Email],
                        Subject = "We received your message — PROTOSCEND",
                        HtmlBody = BuildAutoReplyHtml(model)
                    });

                    return Results.Ok(new { message = "Sent" });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[EMAIL ERROR] {ex.Message}");
                    return Results.Problem("Failed to send email. Please try again later.");
                }
            });

            await app.RunAsync();
        }

        private static string BuildInternalHtml(ContactRequest m) => $"""
            <!DOCTYPE html><html><head><meta charset="utf-8"/></head>
            <body style="margin:0;padding:0;background:#1a1a1e;font-family:'Segoe UI',Arial,sans-serif;">
              <table width="100%" cellpadding="0" cellspacing="0">
                <tr><td align="center" style="padding:40px 20px;">
                  <table width="600" cellpadding="0" cellspacing="0"
                         style="background:#222227;border:1px solid rgba(224,32,32,0.3);border-top:3px solid #e02020;">
                    <tr><td style="padding:32px 40px 24px;">
                      <p style="margin:0 0 4px;font-size:11px;letter-spacing:3px;color:#e02020;text-transform:uppercase;">NEW ENQUIRY</p>
                      <h1 style="margin:0;font-size:24px;color:#e8e8ec;font-weight:700;letter-spacing:2px;">PROTOSCEND</h1>
                    </td></tr>
                    <tr><td style="padding:0 40px 32px;">
                      <table width="100%" cellpadding="12" cellspacing="0"
                             style="background:#252529;border:1px solid rgba(255,255,255,0.07);">
                        <tr>
                          <td style="color:#606075;font-size:11px;letter-spacing:2px;text-transform:uppercase;width:140px;">Name</td>
                          <td style="color:#e8e8ec;font-size:15px;">{m.FirstName} {m.LastName}</td>
                        </tr>
                        <tr>
                          <td style="color:#606075;font-size:11px;letter-spacing:2px;text-transform:uppercase;">Email</td>
                          <td><a href="mailto:{m.Email}" style="color:#e02020;font-size:15px;">{m.Email}</a></td>
                        </tr>
                        {(!string.IsNullOrWhiteSpace(m.Company) ? $"<tr><td style='color:#606075;font-size:11px;text-transform:uppercase;letter-spacing:2px;'>Company</td><td style='color:#e8e8ec;font-size:15px;'>{m.Company}</td></tr>" : "")}
                        <tr>
                          <td style="color:#606075;font-size:11px;letter-spacing:2px;text-transform:uppercase;">Service</td>
                          <td style="color:#e8e8ec;font-size:15px;">{m.Service}</td>
                        </tr>
                        <tr>
                          <td colspan="2" style="padding-top:16px;">
                            <p style="margin:0 0 8px;color:#606075;font-size:11px;letter-spacing:2px;text-transform:uppercase;">Message</p>
                            <p style="margin:0;color:#e8e8ec;font-size:15px;line-height:1.7;border-left:2px solid #e02020;padding-left:12px;">{m.Message}</p>
                          </td>
                        </tr>
                      </table>
                    </td></tr>
                    <tr><td style="padding:16px 40px 32px;border-top:1px solid rgba(255,255,255,0.07);">
                      <p style="margin:0;font-size:11px;color:#606075;">&copy; 2026 PROTOSCEND (Pty) Ltd | Automated notification.</p>
                    </td></tr>
                  </table>
                </td></tr>
              </table>
            </body></html>
            """;

        private static string BuildAutoReplyHtml(ContactRequest m) => $"""
            <!DOCTYPE html><html><head><meta charset="utf-8"/></head>
            <body style="margin:0;padding:0;background:#1a1a1e;font-family:'Segoe UI',Arial,sans-serif;">
              <table width="100%" cellpadding="0" cellspacing="0">
                <tr><td align="center" style="padding:40px 20px;">
                  <table width="600" cellpadding="0" cellspacing="0"
                         style="background:#222227;border:1px solid rgba(224,32,32,0.3);border-top:3px solid #e02020;">
                    <tr><td style="padding:32px 40px 24px;">
                      <p style="margin:0 0 4px;font-size:11px;letter-spacing:3px;color:#e02020;text-transform:uppercase;">YOUR TURNING POINT IN SOFTWARE</p>
                      <h1 style="margin:0;font-size:24px;color:#e8e8ec;font-weight:700;letter-spacing:2px;">PROTOSCEND</h1>
                    </td></tr>
                    <tr><td style="padding:0 40px 32px;">
                      <h2 style="margin:0 0 16px;color:#e8e8ec;font-size:20px;">Hi {m.FirstName}, we've received your message.</h2>
                      <p style="margin:0 0 16px;color:#a0a0b0;font-size:15px;line-height:1.7;">
                        Thank you for reaching out. One of our team members will review your enquiry
                        and get back to you within <strong style="color:#e8e8ec;">1–2 business days</strong>.
                      </p>
                      <table cellpadding="12" cellspacing="0" width="100%"
                             style="background:#252529;border:1px solid rgba(255,255,255,0.07);border-left:3px solid #e02020;margin-bottom:24px;">
                        <tr><td>
                          <p style="margin:0 0 4px;color:#606075;font-size:11px;letter-spacing:2px;text-transform:uppercase;">Reference</p>
                          <p style="margin:0;color:#e8e8ec;font-size:13px;font-family:monospace;">
                            PSC-{DateTime.UtcNow:yyyyMMdd}-{Math.Abs(m.Email.GetHashCode()) % 9999:D4}
                          </p>
                        </td></tr>
                      </table>
                      <p style="margin:0;color:#a0a0b0;font-size:14px;">Warm regards,<br/><strong style="color:#e8e8ec;">The Protoscend Team</strong></p>
                    </td></tr>
                    <tr><td style="padding:16px 40px 32px;border-top:1px solid rgba(255,255,255,0.07);">
                      <p style="margin:0;font-size:11px;color:#606075;">&copy; 2026 PROTOSCEND (Pty) Ltd | South Africa</p>
                    </td></tr>
                  </table>
                </td></tr>
              </table>
            </body></html>
            """;
    }

    public class ContactRequest
    {
        [Required] public string FirstName { get; set; } = "";
        [Required] public string LastName { get; set; } = "";
        [Required][EmailAddress] public string Email { get; set; } = "";
        public string Company { get; set; } = "";
        public string Service { get; set; } = "";
        [Required][MinLength(10)] public string Message { get; set; } = "";
    }
}
