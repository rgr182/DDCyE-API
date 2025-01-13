# AppSettings Structure

This file contains the structure of the appsettings.json file for your application.

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "string"
  },
  "AppSettings": {
    "Token": "SHA-256 string to validate jwt signing",
    "BackEndUrl": "base url for http requests"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "OpenAI-Identifiers": {
    "FormAssistant": "string"
  }
}
```
