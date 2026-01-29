# SimpleDeploy
A simple deployment service with IIS integrations.

## Description
SimpleDeploy is a simple deployment tool for publishing deployments remotely. It hosts a service that will accept files/jobs to be deployed to a destination server/host, allowing you to run a custom deploy script and upload multiple artifacts via command line or powershell. SimpleDeploy supports IIS integration for seamlessly deploying websites.

## Installing
Download the [latest release files](https://github.com/replaysMike/SimpleDeploy/releases). The installation will require installing the SimpleDeploy Agent which would run on the server/host you are deploying to, and the client application (either the Deploy client console application or Powershell Cmdlet extensions).

The SimpleDeploy Agent runs on port 5001 by default, you will need to allow connections to it in your firewall as needed.

### Status
This repo is currently in alpha status and is under testing so limited documentation and examples are currently available.

### Client Commands

There are two clients available - `Deploy.exe` and `Powershell extensions`.

Artifacts should be added first before submitting a deployment. After deploying, the pending artifacts will be cleared if it was successfully submitted.

### Deploy.exe client

Adding artifacts to a deployment:
```
// add a zip file
.\Deploy.exe --add --deployment-name example.com --file ./MyApplication.zip
// add a deployment script
.\Deploy.exe -a -n example.com -f ./deploy.ps1
```

Submitting a deployment:
```
// specify the website or deployment name to deploy, and let it know which artifact file name that will serve as the deployment script using the --script option
.\Deploy.exe --deploy --script deploy.ps1 --deployment-name example.com --host localhost --username test --password test
```

Submitting a deployment while auto stopping/starting IIS and copying the deployment to the website path:
```
// specify the website or deployment name to deploy, and let it know which artifact file name that will serve as the deployment script using the --script option
.\Deploy.exe --deploy --script deploy.ps1 --deployment-name example.com --domain example.com --host localhost --username test --password test --iis --autocopy
```

Submitting a deployment with adding artifacts inline:
```
// specify the website to deploy, and let it know which artifact file name that will serve as the deployment script using the --script option
.\Deploy.exe --deploy --script deploy.ps1 --file deploy.ps1 *.zip --deployment-name example.com --host localhost --username test --password test
```

Submitting a deployment without specifying deployment script
```
// specify the website to deploy, auto detect the detect the deployment script name based on presets (deploy.ps1, deploy.bat, deploy.cmd)
.\Deploy.exe --deploy --file deploy.ps1 *.zip --deployment-name example.com --host localhost --username test --password test
```

Submitting a deployment interactively to view the log output
```
// it will wait until deployment is complete and output the deployment script logs
.\Deploy.exe --deploy --script deploy.ps1 --deployment-name example.com --host localhost --username test --password test --interactive
```

Submitting a deployment and auto extract compressed files before running the deployment script
```
// compressed files included in the artifacts will be decompressed before running the deployment script. Saves you from having to extract them inside your script.
.\Deploy.exe --deploy --script deploy.ps1 --deployment-name example.com --host localhost --username test --password test --autoextract
```

Submitting a deployment and ignoring ssl certificate errors:
```
// if your ssl certificate has issues, you can opt to ignore the certificate validity check
.\Deploy.exe --deploy --script deploy.ps1 --deployment-name example.com --host localhost --username test --password test --ignorecert
```

Listing the artifacts currently added for an upcoming deployment:
```
.\Deploy.exe --get --deployment-name example.com
```

Removing an artifact:
```
.\Deploy.exe --remove --file ./MyApplication.zip --deployment-name example.com
```

All options:
| Option | Description |
| ------ | ------- |
| -a, --add | Add an artifact to the deployment |
| -r, --remove | Remove an artifact from the deployment |
| -g, --get | Get the artifacts for the deployment |
| -d, --deploy | Deploy the website |
| -f, --file | Specify the filename(s) of an artifact |
| -n, --deployment-name | Required. Specify a name for the deployment |
| -w, --domain | Specify the domain name for the deployment. If not provided, DeploymentName will be used |
| -s, --script | Specify the deployment script, filename or content (default: deploy.ps1) |
| -h, --host | Specify the host to deploy to (ip or hostname) |
| -u, --username | Specify the username for deployment |
| -p, --password | Specify the password for deployment |
| -t, --token | Specify the authentication token for deployment |
| --port | Specify the port number of the deployment host (default: 5001) |
| --timeout | Specify the connection timeout (default: 5 seconds) |
| --request-timeout | Specify the request timeout (default: 300 seconds) |
| -v, --verbose | Specify verbose output |
| --autocopy | Automatically copy files to destination after running deployment (default: false) |
| --autoextract | Automatically extract compressed files before running deployment (default: false) |
| -i, --ignorecert | Ignore any SSL certificate errors |
| --interactive | Run deployment in interactive mode to view the output |
| --iis | Specify this is an IIS website, stop and start the website during deployment. |
| --help | Display this help screen. |
| --version | Display version information. |

### Powershell client

Installing the powershell extensions can be tricky depending on the version of Powershell you are using.

Adding the extensions:
```ps
Import-Module SimpleDeploy.Cmdlet
```

Adding artifacts to a deployment:
```ps
Add-Artifact .\MyApplication.zip example.com
Add-Artifact --File .\deploy.ps1 --DeploymentName example.com
Add-Artifact --File ".\deploy.ps1","*.zip" --DeploymentName example.com
```

Submitting a deployment:
```ps
# specify the website to deploy, and let it know which artifact name that will serve as the deployment script using the --Script option
Deploy-Website example.com --Script deploy.ps1 --Host localhost --Username test --Password test --IIS --AutoCopy
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

The default installation path for the SimpleDeploy Agent service is `C:\Program Files\SimpleDeploy Agent`. You should add either username/password or auth token based authentication in the config file named `appsettings.json`. We plan to support public key authentication support in the near future.

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
    "JobsFolder": "Jobs",
    "BackupsFolder": "Backups",
    "MaxDeploymentSize": 1048576000, // 1GB
    "MinFreeSpace": 104857600, // 100mb
    "MaxBackupFiles": 10,
    "CleanupAfterDeploy": true,
    "RestartAfterDeploy": true,
    "DeploymentNames": {
      //"Allow": [ "*" ],
      "Allow": [ "example.com" ],
      // optional per deployment configurations
      "Configurations": [
        {
          "Name": "example.com",
          "Domain": "example.com",
          "IIS": true,
          "AutoCopy": true,
          "AutoExtract": true,
          "Backup": true
        }
      ]
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
