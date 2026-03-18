# Copilot Instructions

## Project Guidelines
- User prefers streaming UI population (incremental results) and wants filesystem caching for root directories in Explorer view model.
- User prefers pre-normalizing file extensions at startup to avoid repeated normalization during each search/filter operation.
- User prefers a compact file-list hierarchy indication with a single indentation level for items that are in subfolders, without distinguishing multiple depth levels.
- Whenever icons are used in the UI, without a label, include a tooltip for clarity.
- Virtual tags should represent purely inherited tags from ancestor folders, not only missing project-name tags.