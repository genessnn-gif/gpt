(() => {
  "use strict";

  const SETTINGS_KEY = "chatgptDomTrimmerSettings";
  const DEFAULT_SETTINGS = {
    enabled: true,
    maxVisible: 5,
    suspend: false
  };

  const HEURISTICS = {
    selectors: [
      "[data-message-author-role]",
      "[data-message-id]",
      "article[data-testid='conversation-turn']",
      "article"
    ],
    minimumMessages: 2,
    bannerId: "chatgpt-dom-trimmer-banner",
    restoreBatch: 10,
    scrollRevealThreshold: 120
  };

  const state = {
    settings: { ...DEFAULT_SETTINGS },
    hiddenBefore: [],
    hiddenAfter: [],
    trimming: false,
    scheduled: false,
    panicDisabled: false,
    browsingOlder: false,
    lastRevealAt: 0,
    observer: null,
    scrollContainer: null
  };

  const storage = chrome?.storage?.sync || chrome?.storage?.local;

  function loadSettings() {
    if (!storage) return Promise.resolve();
    return new Promise((resolve) => {
      storage.get([SETTINGS_KEY], (result) => {
        const stored = result?.[SETTINGS_KEY] || {};
        state.settings = {
          ...DEFAULT_SETTINGS,
          ...stored
        };
        resolve();
      });
    });
  }

  function saveSettings(next) {
    state.settings = { ...state.settings, ...next };
    if (storage) {
      storage.set({ [SETTINGS_KEY]: state.settings });
    }
  }

  function scheduleTrim(reason = "") {
    if (state.scheduled || state.trimming) return;
    if (!state.settings.enabled || state.settings.suspend || state.panicDisabled) return;
    state.scheduled = true;
    const run = () => {
      state.scheduled = false;
      performTrim(reason);
    };
    if ("requestIdleCallback" in window) {
      window.requestIdleCallback(run, { timeout: 1000 });
    } else {
      window.setTimeout(run, 200);
    }
  }

  function scheduleRevealOlder() {
    if (!state.settings.enabled || state.settings.suspend || state.panicDisabled) return;
    if (Date.now() - state.lastRevealAt < 750) return;
    state.lastRevealAt = Date.now();
    const run = () => revealOlder();
    if ("requestIdleCallback" in window) {
      window.requestIdleCallback(run, { timeout: 1000 });
    } else {
      window.setTimeout(run, 200);
    }
  }

  function findMessageNodes(root) {
    for (const selector of HEURISTICS.selectors) {
      const nodes = Array.from(root.querySelectorAll(selector));
      const filtered = nodes.filter((node) => {
        if (!(node instanceof HTMLElement)) return false;
        if (node.getAttribute("data-dom-trimmer") === "ignore") return false;
        const text = node.textContent?.trim();
        return Boolean(text);
      });
      if (filtered.length >= HEURISTICS.minimumMessages) {
        return filtered;
      }
    }
    return [];
  }

  function findConversationRoot() {
    const main = document.querySelector("main");
    if (main) {
      const nodes = findMessageNodes(main);
      if (nodes.length) {
        return { root: main, nodes };
      }
    }

    const bodyNodes = findMessageNodes(document.body);
    if (bodyNodes.length) {
      return { root: document.body, nodes: bodyNodes };
    }

    return null;
  }

  function findScrollContainer(start) {
    let el = start?.parentElement;
    while (el && el !== document.body) {
      const style = window.getComputedStyle(el);
      if (["auto", "scroll"].includes(style.overflowY) && el.scrollHeight > el.clientHeight) {
        return el;
      }
      el = el.parentElement;
    }
    return document.scrollingElement || document.documentElement;
  }

  function ensureBanner(root, hiddenCount) {
    const firstMessage = findMessageNodes(root)[0];
    if (!firstMessage) return;
    let banner = root.querySelector(`#${HEURISTICS.bannerId}`);

    if (hiddenCount <= 0) {
      if (banner) banner.remove();
      return;
    }

    if (!banner) {
      banner = document.createElement("div");
      banner.id = HEURISTICS.bannerId;
      banner.setAttribute("data-dom-trimmer", "ignore");
      banner.style.cssText =
        "position: relative; padding: 8px 12px; margin: 8px 0; font-size: 12px; " +
        "background: rgba(255, 199, 0, 0.15); border: 1px solid rgba(255, 199, 0, 0.4); " +
        "border-radius: 8px; color: #7a5b00; cursor: pointer; text-align: center;";
      banner.addEventListener("click", scheduleRevealOlder);
    }

    banner.textContent = `Older messages hidden: ${hiddenCount}`;

    if (!banner.parentElement) {
      root.insertBefore(banner, firstMessage);
    }
  }

  function moveNodesToHidden(nodes, bucket) {
    nodes.forEach((node) => {
      bucket.push(node);
      node.remove();
    });
  }

  function moveHiddenToDom(nodes, root) {
    if (!nodes.length) return;
    const firstVisible = findMessageNodes(root)[0];
    if (!firstVisible) return;
    nodes.forEach((node) => {
      root.insertBefore(node, firstVisible);
    });
  }

  function performTrim(reason = "") {
    if (state.trimming) return;
    if (!state.settings.enabled || state.settings.suspend || state.panicDisabled) return;

    const conversation = findConversationRoot();
    if (!conversation) return;
    const { root } = conversation;
    const messages = findMessageNodes(root);
    if (messages.length < HEURISTICS.minimumMessages) return;

    state.trimming = true;
    try {
      const maxVisible = Math.max(1, Number(state.settings.maxVisible) || DEFAULT_SETTINGS.maxVisible);
      if (!state.browsingOlder) {
        state.hiddenAfter = [];
      }

      if (messages.length > maxVisible) {
        const overflow = messages.length - maxVisible;
        const toRemove = state.browsingOlder
          ? messages.slice(messages.length - overflow)
          : messages.slice(0, overflow);

        if (state.browsingOlder) {
          moveNodesToHidden(toRemove, state.hiddenAfter);
        } else {
          moveNodesToHidden(toRemove, state.hiddenBefore);
        }
      }

      ensureBanner(root, state.hiddenBefore.length);
      state.scrollContainer = findScrollContainer(messages[0]);
    } finally {
      state.trimming = false;
    }
  }

  function revealOlder() {
    const conversation = findConversationRoot();
    if (!conversation) return;
    const { root } = conversation;

    if (!state.hiddenBefore.length) {
      ensureBanner(root, 0);
      return;
    }

    const batch = Math.min(HEURISTICS.restoreBatch, state.hiddenBefore.length);
    const toRestore = state.hiddenBefore.splice(-batch);
    moveHiddenToDom(toRestore, root);

    state.browsingOlder = true;

    if (!state.settings.suspend && state.settings.enabled && !state.panicDisabled) {
      scheduleTrim("reveal-older");
    }

    ensureBanner(root, state.hiddenBefore.length);
  }

  function restoreAllHidden() {
    const conversation = findConversationRoot();
    if (!conversation) return;
    const { root } = conversation;
    moveHiddenToDom(state.hiddenBefore.splice(0), root);
    moveHiddenToDom(state.hiddenAfter.splice(0), root);
    state.browsingOlder = false;
    ensureBanner(root, 0);
  }

  function handleScroll() {
    if (!state.scrollContainer) return;
    if (state.scrollContainer.scrollTop <= HEURISTICS.scrollRevealThreshold) {
      scheduleRevealOlder();
    }
  }

  function observeMutations() {
    if (state.observer) state.observer.disconnect();
    const conversation = findConversationRoot();
    if (!conversation) return;

    state.observer = new MutationObserver(() => {
      if (state.trimming) return;
      scheduleTrim("mutation");
    });

    state.observer.observe(conversation.root, {
      childList: true,
      subtree: true
    });

    state.scrollContainer = findScrollContainer(conversation.nodes[0]);
    if (state.scrollContainer) {
      state.scrollContainer.removeEventListener("scroll", handleScroll);
      state.scrollContainer.addEventListener("scroll", handleScroll, { passive: true });
    }
  }

  function handleRuntimeMessage(message, sender, sendResponse) {
    if (!message || typeof message !== "object") return;

    switch (message.type) {
      case "updateSettings":
        saveSettings(message.payload || {});
        if (state.settings.suspend) {
          restoreAllHidden();
        } else {
          scheduleTrim("settings-update");
        }
        sendResponse?.({ ok: true });
        break;
      case "panicDisable":
        state.panicDisabled = Boolean(message.payload?.disabled);
        if (state.panicDisabled) {
          restoreAllHidden();
        } else {
          scheduleTrim("panic-enable");
        }
        sendResponse?.({ ok: true });
        break;
      default:
        break;
    }
  }

  function init() {
    loadSettings().then(() => {
      observeMutations();
      scheduleTrim("init");
    });

    chrome.runtime.onMessage.addListener(handleRuntimeMessage);

    if (storage?.onChanged) {
      storage.onChanged.addListener((changes) => {
        if (changes[SETTINGS_KEY]) {
          state.settings = {
            ...DEFAULT_SETTINGS,
            ...(changes[SETTINGS_KEY].newValue || {})
          };
          if (state.settings.suspend) {
            restoreAllHidden();
          } else {
            scheduleTrim("storage-change");
          }
        }
      });
    }
  }

  if (document.readyState === "complete" || document.readyState === "interactive") {
    init();
  } else {
    window.addEventListener("DOMContentLoaded", init, { once: true });
  }
})();
