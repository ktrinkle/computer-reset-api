### Computer Reset Liquidation API
This is the API project for the Computer Reset liquidation website.

### Architecture
This is a .NET core 3 project, with Angular UI. The Angular UI is a static landing page that exists to provide an unsecured landing page, as well as the Facebook required privacy policy.

The .NET core APIs are behind JWT authentication. Since the main UI uses built-in Azure Facebook easy auth, we don't have the auth token available yet. A future project is to look at using https://github.com/MaximRouiller/MaximeRouiller.Azure.AppService.EasyAuth to replace the JWT logic.

Deploy is to Azure App Service. The back-end database is Postgres 11.

### Usage
Most of the APIs are pretty self-explanatory. Most require authentication, but the ones that don't are tied to creating the JWT token, or for the embedded UI landing page to show or hide the signup button based on an app setting in Azure.