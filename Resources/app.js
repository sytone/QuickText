const pad = document.getElementById('pad');
const copyBtn = document.getElementById('copy-btn');
const clearBtn = document.getElementById('clear-btn');
const settingsBtn = document.getElementById('settings-btn');
const closeBtn = document.getElementById('close-btn');
const titleBar = document.getElementById('title-bar');
const linkBar = document.getElementById('link-bar');
const measurer = document.getElementById('measurer');
const windowFrame = document.getElementById('window-frame');
let isCollapsed = false;

const settingsPanel = document.getElementById('settings-panel');
const closeSettingsBtn = document.getElementById('close-settings-btn');
const exitAppBtn = document.getElementById('exit-app-btn');
const autosaveToggle = document.getElementById('autosave-toggle');
const copyHotkeyInput = document.getElementById('copy-hotkey-input');
const closeHotkeyInput = document.getElementById('close-hotkey-input');
const exitHotkeyInput = document.getElementById('exit-hotkey-input');
const globalHotkeyInput = document.getElementById('global-hotkey-input');
const opacitySlider = document.getElementById('opacity-slider');
const opacityValue = document.getElementById('opacity-value');
const minWidthInput = document.getElementById('min-width-input');
const minHeightInput = document.getElementById('min-height-input');
const maxHeightInput = document.getElementById('max-height-input');

// Local configuration state
let settings = {
  autosave: true,
  copyHotkey: 'CTRL+SHIFT+C',
  closeHotkey: 'ESCAPE',
  exitHotkey: 'CTRL+Q',
  globalHotkey: 'not set',
  opacity: 1.0,
  minWidth: 600,
  minHeight: 300,
  maxHeight: 2000
};

// Helper to send message to WPF host
function postToHost(data) {
  if (window.chrome && window.chrome.webview) {
    window.chrome.webview.postMessage(data);
  }
}

// Window Dragging
let dragStart = null;

function isTitleBarControl(target) {
  return target.closest('.btn, input, .switch');
}

titleBar.addEventListener('mousedown', (e) => {
  if (e.button === 0 && !isTitleBarControl(e.target)) {
    dragStart = { x: e.screenX, y: e.screenY };
  }
});

window.addEventListener('mousemove', (e) => {
  if (!dragStart) return;
  if ((e.buttons & 1) === 0) {
    dragStart = null;
    return;
  }
  // Leave stationary clicks in WebView2 so it can recognize a double-click.
  if (Math.abs(e.screenX - dragStart.x) >= 4 || Math.abs(e.screenY - dragStart.y) >= 4) {
    dragStart = null;
    postToHost('drag');
  }
});

window.addEventListener('mouseup', () => { dragStart = null; });
window.addEventListener('blur', () => { dragStart = null; });

titleBar.addEventListener('dblclick', (e) => {
  if (e.button !== 0 || isTitleBarControl(e.target)) return;
  setCollapsed(!isCollapsed);
});

function setCollapsed(collapsed) {
  isCollapsed = collapsed;
  windowFrame.classList.toggle('collapsed', collapsed);
  titleBar.title = collapsed
    ? 'Drag to move; double-click to expand'
    : 'Drag to move; double-click to collapse';
  if (collapsed && settingsPanel.classList.contains('open')) {
    toggleSettings();
  } else {
    triggerResize();
  }
  if (!collapsed) pad.focus();
}

// Close Application Button
closeBtn.addEventListener('click', () => {
  if (settings.globalHotkey && settings.globalHotkey !== 'not set') {
    postToHost('hide');
  } else {
    postToHost('close');
  }
});

// Exit App Button inside Settings
exitAppBtn.addEventListener('click', () => {
  postToHost('close');
});

// Map slider value (0-100) to opacity (0.2-1.0)
function sliderValueToOpacity(val) {
  if (val <= 50) {
    return 0.20 + (val / 50) * 0.30;
  } else {
    return 0.50 + ((val - 50) / 50) * 0.50;
  }
}

// Map opacity (0.2-1.0) to slider value (0-100)
function opacityToSliderValue(opacity) {
  if (opacity <= 0.50) {
    return Math.round((opacity - 0.20) / 0.30 * 50);
  } else {
    return Math.round(50 + (opacity - 0.50) / 0.50 * 50);
  }
}

// Toggle settings modal
function toggleSettings() {
  if (isCollapsed && !settingsPanel.classList.contains('open')) setCollapsed(false);
  const isOpen = settingsPanel.classList.toggle('open');
  if (isOpen) {
    settingsBtn.classList.add('active');
    document.getElementById('content').classList.add('settings-open');
    autosaveToggle.checked = settings.autosave;
    copyHotkeyInput.value = settings.copyHotkey || 'None';
    closeHotkeyInput.value = settings.closeHotkey || 'None';
    exitHotkeyInput.value = settings.exitHotkey || 'None';
    globalHotkeyInput.value = settings.globalHotkey || 'None';
    
    // Set opacity slider value
    const currentOpacity = settings.opacity !== undefined ? settings.opacity : 1.0;
    opacitySlider.value = opacityToSliderValue(currentOpacity);
    opacityValue.textContent = Math.round(currentOpacity * 100) + '%';

    // Set window size values
    minWidthInput.value = settings.minWidth !== undefined ? settings.minWidth : 600;
    minHeightInput.value = settings.minHeight !== undefined ? settings.minHeight : 300;
    maxHeightInput.value = settings.maxHeight !== undefined ? settings.maxHeight : 2000;
  } else {
    settingsBtn.classList.remove('active');
    document.getElementById('content').classList.remove('settings-open');
    pad.focus();
  }
  // Immediately trigger resize to expand/shrink window for settings panel
  triggerResize();
}

settingsBtn.addEventListener('click', toggleSettings);
closeSettingsBtn.addEventListener('click', toggleSettings);

autosaveToggle.addEventListener('change', () => {
  settings.autosave = autosaveToggle.checked;
  saveSettings();
  if (settings.autosave) {
    postToHost({ type: 'save_note', text: pad.value });
  } else {
    postToHost({ type: 'clear_note' });
  }
});

// Opacity Slider handler
opacitySlider.addEventListener('input', () => {
  const sliderVal = parseInt(opacitySlider.value);
  const opacity = sliderValueToOpacity(sliderVal);
  opacityValue.textContent = Math.round(opacity * 100) + '%';
  postToHost({ type: 'opacity', value: opacity });
});

opacitySlider.addEventListener('change', () => {
  const sliderVal = parseInt(opacitySlider.value);
  settings.opacity = sliderValueToOpacity(sliderVal);
  saveSettings();
});

// Window Size limits change handler
function handleSizeChange() {
  let minW = parseInt(minWidthInput.value) || 600;
  let minH = parseInt(minHeightInput.value) || 300;
  let maxH = parseInt(maxHeightInput.value) || 2000;

  // Enforce bounds:
  // - minWidth: 500 to 1920
  // - minHeight: 150 to 1080
  // - maxHeight: 300 to 2000
  minW = Math.max(500, Math.min(1920, minW));
  minH = Math.max(150, Math.min(1080, minH));
  maxH = Math.max(300, Math.min(2000, maxH));

  // Ensure minHeight <= maxHeight
  if (minH > maxH) {
    if (this === minHeightInput) {
      maxH = minH;
    } else {
      minH = maxH;
    }
  }

  minWidthInput.value = minW;
  minHeightInput.value = minH;
  maxHeightInput.value = maxH;

  settings.minWidth = minW;
  settings.minHeight = minH;
  settings.maxHeight = maxH;

  saveSettings();
  triggerResize();
}

minWidthInput.addEventListener('change', handleSizeChange);
minHeightInput.addEventListener('change', handleSizeChange);
maxHeightInput.addEventListener('change', handleSizeChange);

// Hotkey recording UI registration
let activeHotkeyRecordingInput = null;
let activeSettingKey = null;

function registerHotkeyInput(inputEl, settingKey) {
  inputEl.addEventListener('focus', () => {
    inputEl.value = 'Press keys...';
    activeHotkeyRecordingInput = inputEl;
    activeSettingKey = settingKey;
  });
  inputEl.addEventListener('blur', () => {
    inputEl.value = settings[settingKey] || 'None';
    activeHotkeyRecordingInput = null;
    activeSettingKey = null;
  });
}

registerHotkeyInput(copyHotkeyInput, 'copyHotkey');
registerHotkeyInput(closeHotkeyInput, 'closeHotkey');
registerHotkeyInput(exitHotkeyInput, 'exitHotkey');
registerHotkeyInput(globalHotkeyInput, 'globalHotkey');

// Keyboard event listener for shortcuts and recording
window.addEventListener('keydown', (e) => {
  // If we are actively recording a hotkey
  if (activeHotkeyRecordingInput) {
    e.preventDefault();
    e.stopPropagation();
    
    const mainKey = e.key.toUpperCase();
    if (mainKey === 'BACKSPACE' || mainKey === 'DELETE') {
      settings[activeSettingKey] = 'not set';
      activeHotkeyRecordingInput.value = 'not set';
      saveSettings();
      activeHotkeyRecordingInput.blur();
      return;
    }
    
    let keys = [];
    if (e.ctrlKey) keys.push('CTRL');
    if (e.shiftKey) keys.push('SHIFT');
    if (e.altKey) keys.push('ALT');
    
    if (mainKey !== 'CONTROL' && mainKey !== 'SHIFT' && mainKey !== 'ALT' && mainKey !== 'OS') {
      const keyName = mainKey === 'ESCAPE' ? 'ESCAPE' : mainKey;
      keys.push(keyName);
      const hotkeyStr = keys.join('+');
      
      settings[activeSettingKey] = hotkeyStr;
      activeHotkeyRecordingInput.value = hotkeyStr;
      saveSettings();
      activeHotkeyRecordingInput.blur();
    }
    return;
  }

  // Normal hotkeys evaluation
  const pressedStr = getPressedHotkeyString(e);
  if (pressedStr) {
    if (settings.exitHotkey !== 'not set' && pressedStr === settings.exitHotkey) {
      e.preventDefault();
      postToHost('close');
    } else if (settings.copyHotkey !== 'not set' && pressedStr === settings.copyHotkey) {
      e.preventDefault();
      triggerCopy();
    } else if (settings.closeHotkey !== 'not set' && pressedStr === settings.closeHotkey) {
      e.preventDefault();
      if (settingsPanel.classList.contains('open')) {
        toggleSettings();
      } else {
        if (settings.globalHotkey && settings.globalHotkey !== 'not set') {
          postToHost('hide');
        } else {
          postToHost('close');
        }
      }
    }
  }
});

function getPressedHotkeyString(e) {
  let keys = [];
  if (e.ctrlKey) keys.push('CTRL');
  if (e.shiftKey) keys.push('SHIFT');
  if (e.altKey) keys.push('ALT');
  
  const mainKey = e.key.toUpperCase();
  if (mainKey !== 'CONTROL' && mainKey !== 'SHIFT' && mainKey !== 'ALT' && mainKey !== 'OS') {
    const keyName = mainKey === 'ESCAPE' ? 'ESCAPE' : mainKey;
    keys.push(keyName);
    return keys.join('+');
  }
  return '';
}

function saveSettings() {
  postToHost({ type: 'save_settings', settings: settings });
}

// Clear all text
clearBtn.addEventListener('click', () => {
  pad.value = '';
  pad.focus();
  detectLinks();
  triggerResize();
  
  if (settings.autosave) {
    postToHost({ type: 'clear_note' });
  }
  
  clearBtn.classList.add('btn-success');
  const originalText = clearBtn.textContent;
  clearBtn.textContent = 'Cleared';
  setTimeout(() => {
    clearBtn.textContent = originalText;
    clearBtn.classList.remove('btn-success');
  }, 1000);
});

// Copy action with animation
function triggerCopy() {
  if (!pad.value) return;
  postToHost({ type: 'copy', text: pad.value });
  const originalText = copyBtn.textContent;
  copyBtn.textContent = 'Copied';
  copyBtn.classList.add('btn-success');
  setTimeout(() => {
    copyBtn.textContent = originalText;
    copyBtn.classList.remove('btn-success');
  }, 1000);
}

copyBtn.addEventListener('click', triggerCopy);

// Capture Tab key
pad.addEventListener('keydown', function (e) {
  if (e.key === 'Tab') {
    e.preventDefault();
    const start = this.selectionStart;
    const end = this.selectionEnd;
    this.value = this.value.substring(0, start) + "\t" + this.value.substring(end);
    this.selectionStart = this.selectionEnd = start + 1;
    triggerResize();
  }
});

// Detect links and resize on input
pad.addEventListener('input', () => {
  detectLinks();
  triggerResize();
  if (settings.autosave) {
    postToHost({ type: 'save_note', text: pad.value });
  }
});

function detectLinks() {
  const text = pad.value;
  const urlRegex = /(https?:\/\/[^\s]+)/g;
  
  const urlPositions = [];
  let match;
  while ((match = urlRegex.exec(text)) !== null) {
    urlPositions.push({
      url: match[0],
      start: match.index,
      end: match.index + match[0].length
    });
  }
  
  linkBar.innerHTML = '';
  const contentEl = document.getElementById('content');
  
  if (urlPositions.length > 0) {
    linkBar.style.display = 'flex';
    contentEl.classList.add('has-links');
    urlPositions.forEach((posInfo, idx) => {
      const a = document.createElement('a');
      a.className = 'link-pill';
      a.href = posInfo.url;
      a.target = '_blank';
      
      let display = posInfo.url.replace(/https?:\/\/(www\.)?/, '');
      if (display.length > 20) display = display.substring(0, 18) + '...';
      a.textContent = (idx + 1) + ' · ' + display;
      
      let originalStart = 0;
      let originalEnd = 0;
      a.addEventListener('mouseenter', () => {
        originalStart = pad.selectionStart;
        originalEnd = pad.selectionEnd;
        pad.focus();
        pad.setSelectionRange(posInfo.start, posInfo.end);
      });
      a.addEventListener('mouseleave', () => {
        pad.setSelectionRange(originalStart, originalEnd);
      });
      
      a.addEventListener('click', (e) => {
        e.preventDefault();
        postToHost({ type: 'open_url', url: posInfo.url });
      });
      linkBar.appendChild(a);
    });
    
    // Position watermark above links dynamically based on actual linkbar rendering height
    setTimeout(() => {
      const linkBarHeight = linkBar.offsetHeight;
      document.getElementById('watermark').style.bottom = (linkBarHeight + 24) + 'px';
    }, 0);
  } else {
    linkBar.style.display = 'none';
    contentEl.classList.remove('has-links');
    document.getElementById('watermark').style.bottom = '24px';
  }
}

// Dynamically resize window based on content scrollHeight
function triggerResize() {
  const titleHeight = titleBar.getBoundingClientRect().height;
  const width = settings.minWidth !== undefined ? settings.minWidth : 600;
  if (isCollapsed) {
    const frameStyle = getComputedStyle(windowFrame);
    const borderHeight = parseFloat(frameStyle.borderTopWidth) + parseFloat(frameStyle.borderBottomWidth);
    postToHost({ type: 'resize', width: width, height: titleHeight + borderHeight });
    return;
  }
  const linkHeight = linkBar.style.display !== 'none' ? linkBar.offsetHeight : 0;
  
  // Set measurer width dynamically to match actual textarea width
  measurer.style.width = (width - 24) + 'px';
  
  // Measure textarea height accurately using a temporary auto height
  measurer.textContent = pad.value + '\n';
  const textHeight = Math.max(200, measurer.scrollHeight);
  
  // Total window frame height = title + text + linkbar + padding/border
  const totalRequiredHeight = titleHeight + textHeight + linkHeight + 24;
  
  // Settings panel needs at least 500px to display all settings without clipping
  const isSettingsOpen = settingsPanel.classList.contains('open');
  const minHeightVal = settings.minHeight !== undefined ? settings.minHeight : 300;
  const maxHeightVal = settings.maxHeight !== undefined ? settings.maxHeight : 2000;
  
  const minHeight = isSettingsOpen ? 500 : minHeightVal;
  
  let targetHeight = Math.max(minHeight, Math.min(maxHeightVal, totalRequiredHeight));
  
  postToHost({ type: 'resize', width: width, height: targetHeight });
  
  // Reset pad height inline style to let flexbox stretch it
  pad.style.height = '';
}

// WebView2 message listener from Host
if (window.chrome && window.chrome.webview) {
  window.chrome.webview.addEventListener('message', (e) => {
    const data = e.data;
    if (data && data.type === 'init') {
      if (data.settings) {
        settings = data.settings;
        if (settings.minWidth === undefined) settings.minWidth = 600;
        if (settings.minHeight === undefined) settings.minHeight = 300;
        if (settings.maxHeight === undefined) settings.maxHeight = 2000;
      }
      if (data.noteText !== undefined) {
        pad.value = data.noteText;
      }
      detectLinks();
      triggerResize();
      
      // Apply opacity on UI load
      const currentOpacity = settings.opacity !== undefined ? settings.opacity : 1.0;
      opacitySlider.value = opacityToSliderValue(currentOpacity);
      opacityValue.textContent = Math.round(currentOpacity * 100) + '%';
      
      // Apply hotkey on UI load
      exitHotkeyInput.value = settings.exitHotkey || 'None';
    }
  });
}

// Startup call to signal host we are ready
setTimeout(() => {
  postToHost({ type: 'ready' });
}, 50);
