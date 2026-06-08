# Protoscend — Deployment & Maintenance Guide
 
## Architecture Overview
 
| Layer | Technology | Host | Repo |
|-------|-----------|------|------|
| Frontend | Blazor WASM (pre-built static files) | Cloudflare Pages | `protoscend-web` → branch `published` |
| API | ASP.NET Core Minimal API (Docker) | Render (free tier) | `protoscend-api` → branch `main` |
| Email | Resend API (HTTPS, no SMTP) | — | API key in Render env vars |
| DNS | Cloudflare | — | Domain: `protoscend.com` |
 
---
 
## Local File Paths
 
```
C:\Users\Emile Hugo\My Files\Personal\Projects\Protoscend\Website\
├── Protoscend\              ← Blazor WASM frontend project
│   ├── release\wwwroot\     ← Published static output (git repo → branch: published)
│   └── Services\
│       ├── ContactApiService.cs   ← Posts form data to the API
│       └── EmailService.cs        ← DEAD CODE — not used, can be deleted
└── Protoscend.Api\          ← ASP.NET Core API project (git repo → branch: main)
    └── Program.cs           ← All API logic lives here
```
 
 
## Deploying the API (Render)
 
Use this whenever you change anything in `Protoscend.Api`.
 
```bash
cd "C:\Users\Emile Hugo\My Files\Personal\Projects\Protoscend\Website\Protoscend.Api"
git add .
git commit -m "describe your change"
git push origin main
```
 
Render detects the push and automatically rebuilds the Docker container and redeploys. Watch progress at **dashboard.render.com → protoscend-api → Logs**.
 
### When the API deploy fails
 
Check the Render logs. Common causes:
 
| Error in logs | Fix |
|--------------|-----|
| `CS0246: type not found 'Resend'` | Run `dotnet add package Resend` in the API folder, then commit and push the `.csproj` |
| `[EMAIL ERROR] ...` | Check Render environment variables (see below) |
| Build succeeds but emails fail | See Email Troubleshooting section |
 
---

## Render Environment Variables
 
Go to **dashboard.render.com → protoscend-api → Environment** to view or change these.
 
| Key | Value | Notes |
|-----|-------|-------|
| `Resend__ApiKey` | `re_xxxxxxxxxxxx` | From resend.com/api-keys. **Never commit this to git.** |
| `Email__To` | `emile03.hugo@outlook.com` | Where enquiry emails are delivered |
 
> **Note:** ASP.NET Core maps double-underscore env vars (`Resend__ApiKey`) to colon-separated config keys (`Resend:ApiKey`) automatically.
 
### If you need to rotate the Resend API key
1. Go to **resend.com → API Keys** → delete the old key → create a new one
2. Update `Resend__ApiKey` in Render environment variables
3. Render will automatically restart the service — no redeploy needed
---
 
## Email System (Resend)
 
### How it works
1. User submits the contact form on the website
2. Blazor calls `ContactApiService.SubmitAsync()` which POSTs JSON to `https://protoscend-api.onrender.com/api/contact`
3. The API sends two emails via the Resend HTTPS API:
   - **Internal notification** → to `Email__To` (your inbox), with Reply-To set to the enquirer
   - **Auto-reply** → to the enquirer's email address
4. Both emails send from `noreply@protoscend.com`
### Why Resend instead of SMTP
Render's free tier **blocks all outbound SMTP connections** (ports 25, 465, 587). Resend uses HTTPS so it works fine.
 
### Resend domain
- Domain `protoscend.com` is verified in Resend
- DNS records were added automatically via Cloudflare authorization
- From address: `noreply@protoscend.com`
### Email troubleshooting
 
| Symptom | Cause | Fix |
|---------|-------|-----|
| 500 error, times out after ~100s | SMTP blocked by Render | Already fixed — using Resend now |
| 500 error, fast response (~2s) | Resend API key invalid or missing | Check `Resend__ApiKey` in Render env vars |
| First email sends, second fails with 403 | Sending to unverified address | Domain must be verified in Resend — already done for `protoscend.com` |
| `[EMAIL ERROR]` in Render logs | Check the exact message | Go to Render → Logs and read the full error |
 
---

## Common Scenarios
 
### "The site is broken / showing error for some users but not others"
This is almost always a **stale browser cache** issue. The user has old `_framework` files cached.
- They can fix it by clearing site data for `protoscend.com` in their browser settings
- Prevented long-term by the `_headers` file which sets `Cache-Control: no-cache` for `/_framework/*`
- After any frontend deploy, go to **Cloudflare → Caching → Purge Cache**
### "The API is slow to respond the first time"
Render's free tier **spins down after 15 minutes of inactivity**. The first request after sleep takes 30–60 seconds to wake up. This is normal on the free tier. Upgrade to a paid Render plan ($7/month) to eliminate cold starts.
 
### "I need to add a NuGet package to the API"
```bash
cd "C:\Users\Emile Hugo\My Files\Personal\Projects\Protoscend\Website\Protoscend.Api"
dotnet add package PackageName
git add Protoscend.Api.csproj
git commit -m "Add PackageName package"
git push origin main
```
Always verify the package was actually added: `findstr /i "PackageName" Protoscend.Api.csproj`
 
### "I want to send from noreply@protoscend.co.za instead"
1. Add `protoscend.co.za` as a domain in **resend.com/domains**
2. Authorize the DNS records in Cloudflare
3. Wait for verification
4. Update both `From` fields in `Program.cs` to `noreply@protoscend.co.za`
5. Deploy the API
### "I accidentally committed a secret/password to git"
1. Immediately revoke/rotate the secret (API key, password, etc.)
2. Generate a new one and update Render env vars
3. Remove from git history if needed (ask for help — this requires `git filter-branch` or BFG)
---
 
## Repository Summary
 
| Repo | URL | Branch | What triggers deploy |
|------|-----|--------|---------------------|
| Frontend | github.com/EmilesSchmiles/protoscend-web | `published` | Push to `published` → Cloudflare auto-deploys |
| API | github.com/EmilesSchmiles/protoscend-api | `main` | Push to `main` → Render auto-deploys |
