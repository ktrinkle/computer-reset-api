# Computer Reset Liquidation API
This is the API project for the Computer Reset liquidation website.

## Architecture
This is a .NET core 3 project, with Angular UI. The Angular UI is a static landing page that exists to provide an unsecured landing page, as well as the Facebook required privacy policy.

Deploy is to Azure App Service. The back-end database is Postgres 11.

## Authentication

The .NET core APIs are behind JWT authentication. To support this, we pull back the Facebook Access Token as part of the initial login, and ensure that is a valid token against Facebook's authorization service.

This could probably be handled a little better and with less convolution, but as usual, if it works, run with it.

## Usage
Most of the APIs are pretty self-explanatory. Most require authentication, but the ones that don't are tied to creating the JWT token, or for the embedded UI landing page to show or hide the signup button based on an app setting in Azure.

## User Secrets
User Secrets are enabled. The three that are required for local development are as follows:

1. AppSettings:Secret - used for JWT token
2. AppSettings:DevUserId - hard coded development user ID
3. ConnectionStrings:CRDatabase - Database connection string

These are all set up in the Azure App Service and not stored in the repo.

## Automated tests

There are none. Someday, maybe, I'll feel like it, but since the site is basically done and intended for use for a year and a half, it hasn't been a priority.

## License

This is licensed under the MIT license.