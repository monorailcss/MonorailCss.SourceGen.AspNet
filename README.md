# MonorailCss.SourceGen.AspNet

`MonorailCss.SourceGen.AspNet` is a source generator to help discover in use CSS classes in an ASP.NET application for use with the MonorailCss.Framework library. 

**Here be dragons**: this is woefully incomplete and untested outside of the most simple scenarios. It will only work with static content from cshtml files. HTML files, dynamic setting of CSS classes and probably a decent amount of edge cases haven't even been considered yet. 

## What's Poppin

```mermaid
graph TD
    A[User Edits Razor Files] -->|Compile| B(Source Generator Finds CSS Classes)
    B --> C(Source Generator Creates Method Containing List of all Classes)
    C --> |Runtime| D(In OnInitialize, MonorailCSS is called with those clases to build the stylesheet)
    D --> E(Stylesheet is injected into the head of the blazor app)
    E --> A
```    
