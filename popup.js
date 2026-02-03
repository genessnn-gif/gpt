const SETTINGS_KEY = "chatgptDomTrimmerSettings";
const DEFAULT_SETTINGS = {
  enabled: true,
  maxVisible: 5,
  suspend: false
};

const storage = chrome?.storage?.sync || chrome?.storage?.local;

const toggleEnabled = document.getElementById("toggle-enabled");
const visibleCount = document.getElementById("visible-count");
const toggleSuspend = document.getElementById("toggle-suspend");
const panicButton = document.getElementById("panic");
const resumeButton = document.getElementById("resume");

function withActiveTab(callback) {
  chrome.tabs.query({ active: true, currentWindow: true }, (tabs) => {
    if (tabs && tabs.length) {
      callback(tabs[0]);
    }
  });
}

function sendMessageToTab(type, payload) {
  withActiveTab((tab) => {
    if (!tab?.id) return;
    chrome.tabs.sendMessage(tab.id, { type, payload });
  });
}

function readSettings() {
  return new Promise((resolve) => {
    if (!storage) {
      resolve({ ...DEFAULT_SETTINGS });
      return;
    }
    storage.get([SETTINGS_KEY], (result) => {
      const stored = result?.[SETTINGS_KEY] || {};
      resolve({ ...DEFAULT_SETTINGS, ...stored });
    });
  });
}

function writeSettings(next) {
  const updated = { ...DEFAULT_SETTINGS, ...next };
  if (storage) {
    storage.set({ [SETTINGS_KEY]: updated });
  }
  sendMessageToTab("updateSettings", updated);
}

function hydrateUI(settings) {
  toggleEnabled.checked = settings.enabled;
  visibleCount.value = settings.maxVisible;
  toggleSuspend.checked = settings.suspend;
}

async function init() {
  const settings = await readSettings();
  hydrateUI(settings);

  toggleEnabled.addEventListener("change", () => {
    writeSettings({ enabled: toggleEnabled.checked });
  });

  visibleCount.addEventListener("change", () => {
    const value = Math.max(1, Number(visibleCount.value || DEFAULT_SETTINGS.maxVisible));
    visibleCount.value = value;
    writeSettings({ maxVisible: value });
  });

  toggleSuspend.addEventListener("change", () => {
    writeSettings({ suspend: toggleSuspend.checked });
  });

  panicButton.addEventListener("click", () => {
    sendMessageToTab("panicDisable", { disabled: true });
  });

  resumeButton.addEventListener("click", () => {
    sendMessageToTab("panicDisable", { disabled: false });
  });
}

init();
