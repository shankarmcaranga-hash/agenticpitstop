# AgenticPITSTOP Layout Components

This repository now includes reusable layout components for the site UI:

- `Views/Shared/_Header.cshtml` - top navigation and search area
- `Views/Shared/_Sidebar.cshtml` - left side menu and status widgets
- `Views/Shared/_Footer.cshtml` - footer with links and copyright

These partials are included from `Views/Shared/_Layout.cshtml`.

Notes:
- The header references `~/images/logo.png` for a logo. Add a logo at that path or adjust the markup.
- Styling uses existing Bootstrap CSS; tune `wwwroot/css/site.css` or `AgenticPITSTOP.styles.css` to match the visual design shown in the provided screenshot.

How to customize:
- Edit the partial views in `Views/Shared` to change structure or content.
- Use `@await Html.PartialAsync("_Header")` / `_Sidebar` / `_Footer` to render the partials from other layouts or pages if needed.

If you want a separate Razor layout for pages that need a different structure, create a new layout file and reference it with `@{ Layout = "~/Views/Shared/_YourLayout.cshtml"; }` at the top of the page.
