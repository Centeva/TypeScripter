# TypeScripter

## What it does
TypeScripter is a tool to generate Typescript classes from c# models.
This is super usefull for our stack (.net / angular2) because we can have typed objects from the server and we don't have to worry about our Typescript models being different than our data models.

## How to use it (see the help command)
`> TypeScripter.exe -h 

Typescripter.

    Usage:
      Typescripter.exe <SETTINGSFILE>
      Typescripter.exe <SOURCE> <DESTINATION> [<APIPATH> [ --httpclient ]]
                       [--files=<FILES> | --class=<CLASSNAMES>]...
      Typescripter.exe ( -h | --help )

    Options:
      --files=<FILES>         Comma seperated list of .dll files to generate models from. [ default: *.client.dll ]
      --class=<CLASSNAMES>    Comma seperated list of controller class names. [ default: ApiController ]
      --httpclient            Generated data service will use the new HttpClientModule for angular 4.
      -h --help               Show this screen.

      <SETTINGSFILE>          Path to a json settings file
                                   example settings file contents:
                                       {
                                            "Source": "./",
                                            "Destination": "../app/models/generated",
                                            "Files": [ "*.dll" ],
                                            "ControllerBaseClassNames": [ "ApiController" ],
                                            "ApiRelativePath": "api",
                                            "HttpModule": "HttpClientModule"
                                        }
      <SOURCE>                The path that contains the .dll(s)
      <DESTINATION>           The destination path where the generated models will be placed
      <APIPATH>               The prefix api calls use (leave blank to not generate a data service)