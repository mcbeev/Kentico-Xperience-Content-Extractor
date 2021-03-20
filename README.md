# Kentico Xperience Portal Engine Content Extractor

.Net Console App for extracting content from a Kentico 12 Portal Engine based website and serializing it as standard xml files per page type. This tool is intended to help with the migration from Portal Engine to MVC.

## Supported Minimum Kentico Version

12.0.0

## .NET Framework Version

4.7.2

## Requirements

This app runs on .NET 4.7.2 and requires the .NET 4.7.2. It also assumes you have a Kentico 12 Portal Engine database that your project can connect to and use as a source. The repository includes a sample blog database in the `/database` folder.

## Setup & Run

Setup only requires a few simple steps.

- Clone or fork the repo.
- Restore the `.bak` file from the `/database` folder or use your own Kentico 12 database.
- Update the main app.config `CMSConnectionString` for the console app to talk to the database.
  - Reminder: it is up to you to make sure security is correct from a SQL connection standpoint.
- Update the main app.config `CMSHashStringSalt` to your main Kentico 12 project's HashtringSalt value
  - This ensures macros resolve correctly.
- Update the `pageTypeNameIdentifier` with the codename of the page type you wish to extract.
- Update the `pageTypeColumns` property to have the columns you want to retrieve for the given page type.
- Update the `contentPathToStart` property to the NodeAliasPath of where your content lives (can be empty string for root node).
- Run the application.

## Command Line Arguments

- TODO

## Output

Running the utility will result in a `/Generated` folder that contains sub folders for each page type, and an xml file for each page of the given type that has the content fields populated as children xml nodes.

![Sample Portal Engine Content as xml](/docs/sample-xml.png?raw=true)

It also builds one master xml file that contains all of the nodes per page type that can be used as a source file for the [Kentico Xperience Import Toolkit (KXIT)](https://docs.xperience.io/external-utilities/kentico-xperience-import-toolkit). The KXIT can then be used to import the content of the page types into new MVC page types that you are most likely creating in Kentico Xperience 13 or higher.

![Sample Portal Engine Content All Nodes as xml](/docs/all-nodes-sample-xml.png?raw=true)

Lastly, each image referenced in the content will attempted to be downloaded from the site (if it is running and availabe at a direct URL) to the sub folder of that node. This could be helpful if you want to grab all images and import them into a new Media Library in another Kentico Xperience instance.

![Downloaded Images of the Node in the subfolder](/docs/downloaded-images.png?raw=true)
