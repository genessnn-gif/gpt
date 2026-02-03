# ChatGPT DOM Trimmer (MV3)

Reduce ChatGPT UI lag on long conversations by aggressively trimming older messages from the DOM. The extension keeps a configurable window of recent messages rendered and replaces older content with a lightweight banner.

## Features
- Keeps only the latest **N** messages rendered (default **5**, configurable in the popup).
- Replaces older messages with a banner: **"Older messages hidden: N"**.
- Scroll near the top or click the banner to load older messages in batches.
- Panic button to disable trimming for the current tab.
- Safe fail-open: if message nodes can’t be detected, the script does nothing.
- Uses `MutationObserver` + idle scheduling to avoid interfering with typing or streaming responses.

## Install (Chrome / Edge)
1. Open **chrome://extensions** or **edge://extensions**.
2. Enable **Developer mode**.
3. Click **Load unpacked** and select the extension folder (this repo root).
4. Visit https://chatgpt.com and open a conversation.

## Usage
- **Enabled**: turns trimming on/off.
- **Visible messages**: how many latest messages to keep in the DOM.
- **Suspend trimming**: temporarily stops trimming and restores hidden messages.
- **Panic: Disable for this tab**: turns off trimming for the current tab without changing global settings.

## Heuristics
The content script avoids fragile class selectors. It searches for message nodes using a prioritized list in `content.js`:

```js
const HEURISTICS = {
  selectors: [
    "[data-message-author-role]",
    "[data-message-id]",
    "article[data-testid='conversation-turn']",
    "article"
  ]
};
```

You can tweak these selectors or the `minimumMessages` threshold in `content.js` if ChatGPT’s DOM changes.

## Notes
- Hidden messages are kept in memory (not re-fetched) so they can be restored quickly.
- If restoration fails (DOM structure changes), the banner remains and the extension continues safely.
