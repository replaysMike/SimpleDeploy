# SimpleDeploy
A simple web deployment service for windows/IIS

## Description
SimpleDeploy is an alternative to IIS Web Deploy. It hosts a service that will accept files/jobs to be deployed to a particular website hosted in IIS, allowing you to run a custom deploy script and upload multiple artifacts via command line/powershell.

## Installing
The installation will require installing the SimpleDeploy Agent which would run on the webserver, and the client application (either console or powershell extension). Download both from the releases section.

### Status
This repo is currently in alpha status and is under testing so limited documentation and examples are currently available.

### Client Commands

There are two clients available - `Deploy.exe` and `Powershell extensions`.

Artifacts should be added first before submitting a deployment. After deploying, the pending artifacts will be cleared if it was successfully submitted.

### Deploy.exe client

Adding artifacts to a deployment:
```
// add a zip file
.\Deploy.exe --add --website example.com --file ./MyApplication.zip
// add a deployment script
.\Deploy.exe -a -w example.com -f ./deploy.ps1
```

Submitting a deployment:
```
.\Deploy.exe --deploy --website example.com --host localhost --username test --password test
```

Listing the artifacts currently added for an upcoming deployment:
```
.\Deploy.exe --get --website example.com
```

Removing an artifact:
```
.\Deploy.exe --remove --file ./MyApplication.zip --website example.com
```

### Powershell client

Installing the powershell extensions can be tricky depending on the version of Powershell you are using.

Adding the extensions:
```ps
Import-Module SimpleDeploy.Cmdlet
```

Adding artifacts to a deployment:
```ps
Add-Artifact .\MyApplication.zip example.com
Add-Artifact .\deploy.ps1 example.com
```

Submitting a deployment:
```ps
Deploy-Website example.com --Host localhost --Username test --Password test
```

Listing the artifacts currently added for an upcoming deployment:
```ps
Get-Artifacts example.com
```

Removing an artifact:
```ps
Remove-Artifact ./MyApplication.zip example.com
```

## Configuring the SimpleDeploy Agent service

The default installation path for the SimpleDeploy Agent service is `C:\Program Files\SimpleDeploy Agent`. You should add either username/password or auth token based authentication in the config file named `appsettings.json`

Example configuration:

```json
{
  "Configuration": {
    "IpAddress": "*",
    "Port": 5001,
    "UseHttps": true,
    "Username": "admin",
    "Password": "password",
    "AuthToken": "",
    "IpWhitelist": [ "192.168.0.0/24", "127.0.0.1", "::1" ],
    "WorkingFolder": "c:/SimpleDeploy",
    "CleanupAfterDeploy": true,
    "RestartAfterDeploy": true,
    "Websites": {
      //"Allow": [ "*" ],
      "Allow": [ "example.com" ],
      "Configurations": []
    }
  },
  "NLog": {
    "autoreload": true,
    "variables": {
      "var_logdir": "c:/SimpleDeploy"
    },
    "targets": {
      "console": {
        "type": "Console",
        "layout": "${longdate}|${level:uppercase=true}|${message:withException=true}|${logger}|${all-event-properties}"
      },
      "file": {
        "type": "AsyncWrapper",
        "target": {
          "wrappedFile": {
            "type": "File",
            "fileName": "${var_logdir}/deploy.log",
            "archiveFileName": "${var_logdir}/deploy.{#####}.log",
            "archiveEvery": "Day",
            "archiveAboveSize": "10485760",
            "archiveNumbering": "DateAndSequence",
            "maxArchiveDays": "30"
          }
        }
      }
    },
    "rules": {
      "000_Microsoft": {
        "logger": "Microsoft.*",
        "minLevel": "Warn",
        "writeTo": "File,Console"
      },
      "005_Microsoft": {
        "logger": "Microsoft.*",
        "final": true
      },
      "050_Everything": {
        "logger": "*",
        "minLevel": "Info",
        "writeTo": "File,Console"
      }
    }
  },
  "Logging": {
    "NLog": {
      "IncludeScopes": false,
      "ParseMessageTemplates": true,
      "CaptureMessageProperties": true
    },
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Error"
    }
  }
}
```

### Todo
- [ ] Custom SSL certificate support
- [ ] Public key authentication
- [ ] Better installer support for powershell extensions
- [ ] Unix installer and support for nginx/apache.
