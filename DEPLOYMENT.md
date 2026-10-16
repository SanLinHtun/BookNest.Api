# Free demo deployment: Render + Neon

This repository includes a Dockerfile and a Render Blueprint with `plan: free` explicitly selected. The public HTTPS subdomain is assigned by Render; the name is not reserved by this repository. Render terminates HTTPS and forwards traffic to container port 8080.

## 1. Create the database

1. Sign in at https://console.neon.tech and create a new Free PostgreSQL project. Choose Singapore if available to keep it near the API.
2. On the new, empty database only, use Neon's SQL Editor to execute `database/001_initial.sql`. Do not run this initial schema against an existing populated database.
3. From Neon's connection details, construct an Npgsql connection string:

   `Host=YOUR_NEON_HOST;Port=5432;Database=YOUR_DATABASE;Username=YOUR_USER;Password=YOUR_PASSWORD;SSL Mode=VerifyFull;Channel Binding=Require`

   Use the actual host, database, username and password provided by Neon. A `postgresql://` connection URI cannot be pasted directly into this API's connection-string setting. Keep the credential in Render's secret environment settings, outside Git and chat.

## 2. Deploy the API

1. Push the deployment files to the GitHub repository.
2. In Render choose **New > Blueprint**, select the BookNest repository, and select the branch containing `render.yaml`.
3. Check that the web service plan is **Free**. Enter the Neon connection string for `ConnectionStrings__BookNest` when prompted.
4. Deploy. Copy the actual HTTPS service URL shown by Render.
5. Check `https://YOUR_ASSIGNED_SUBDOMAIN.onrender.com/health` for `{"status":"ok"}` and `/api/v1/books` for a successful database-backed response. A new empty catalog returns no books.
6. Use `https://YOUR_ASSIGNED_SUBDOMAIN.onrender.com` as the mobile clients' base URL. OpenAPI remains enabled only in local Development.

`/health` checks the running API only; the catalog request must also succeed before considering the database connected. The API does not initialize or migrate the database on startup.

## Free demo limitations

- Render Free services sleep after inactivity, so the next request can be slow. Free usage limits apply to both providers.
- Render Free storage is ephemeral. This initial deployment does not persist ASP.NET Data Protection keys, so users can need to sign in again after a restart or deployment. Persist protected keys before a production rollout.
- Uploaded/private book files cannot be kept durably on Render Free's local filesystem. Configure durable private content storage before adding real book files; the existing API currently reads from `Content__RootPath`.
- Live payment processing is not connected. Admin permissions must be configured explicitly through `AdminUserIds__0`, etc.; public registration grants no admin access.
- Free plan references: https://render.com/docs/free and https://neon.com/pricing . Never select a paid upgrade without reviewing its cost.
