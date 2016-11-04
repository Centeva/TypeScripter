# TypeScripter

## What it does
TypeScripter is a tool to generate Typescript classes from c# models.
This is super usefull for our stack (.net / angular2) because we can have typed objects from the server and we don't have to worry about our Typescript models being different than our data models.

## How to use it
TypeScripter is a command line tool that can be used two ways.

### File path way
`> TypeScripter.exe <Options file path>` 

### Legacy way
`> TypeScripter.exe <dll source path> <model target path>`

## Options
Options file should be a JSON file with these properties.
- `Source`: Location the the dll models you want to generate Typescript models from.

- `Destination`: Location that TypeScripter will place your generated Typescript.

- `Files`: [Optional] What dlls do you want to convert. Defaults to "*.Client.dll".

- `ControllerBaseClassNames`: [Optional] The naming convention for your controllers. Defaults to "ApiController".

Example: ```{
    "Source": "C:\\Source\\nrc\\rps\\App.Client\\bin",
    "Destination": "C:\\Source\\App\\Models\\Generated",
    "Files": ["*.Client.dll"],
    "ControllerBaseClassNames": ["ApiController"]
}```