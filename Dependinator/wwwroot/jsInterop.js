export function showPrompt(message) {
  return prompt(message, 'Type anything here');
}

export function listenToWindowResize(elementId, instance, functionName) {
  function resizeEventHandler() {
    instance.invokeMethodAsync(functionName);
  }

  window.addEventListener("resize", resizeEventHandler);
}

export function preventDefaultTouchEvents(elementId) {
  // Prevent default touch events(scrolling, zooming, etc.), handled by the app
  document.getElementById(elementId).addEventListener('touchstart', function (e) {
    e.preventDefault();
  }, { passive: false });
  document.getElementById(elementId).addEventListener('touchmove', function (e) {
    e.preventDefault();
  }, { passive: false });
  document.getElementById(elementId).addEventListener('touchend', function (e) {
    e.preventDefault();
  }, { passive: false });
}

export function clickElement(elementId) {
  document.getElementById(elementId).click();
}

export function postVsCodeMessage(message) {
  if (window.dependinator && typeof window.dependinator.postMessage === "function") {
    window.dependinator.postMessage(message);
    return true;
  }
  return false;
}

export function isVsCodeWebView() {
  return !!(window.dependinator && typeof window.dependinator.postMessage === "function");
}

export function listenToVsCodeMessages(instance, functionName) {
  window.addEventListener("message", (event) => {
    if (!event || !event.data || !event.data.type) {
      return;
    }

    instance.invokeMethodAsync(functionName, event.data);
  });
}

export function getBoundingRectangle(elementId) {
  var element = document.getElementById(elementId);
  if (element == null) {
    return null;
  }
  return element.getBoundingClientRect();
}

export function getWindowSizeDetails(parm) {
  var e = window, a = 'inner';

  if (!('innerWidth' in window)) {
    a = 'client';
    e = document.documentElement || document.body;
  }

  let windowSize =
  {
    innerWidth: e[a + 'Width'],
    innerHeight: e[a + 'Height'],
    screenWidth: window.screen.width,
    screenHeight: window.screen.height
  };

  return windowSize;
}

export function addMouseEventListener(elementId, eventName, instance, functionName) {
  function eventHandler(event) {
    // console.log("DEP: mouse", event);
    instance.invokeMethodAsync(functionName, {
      Type: eventName,
      TargetId: event.target.id,
      OffsetX: event.offsetX,
      OffsetY: event.offsetY,
      ClientX: event.clientX,
      ClientY: event.clientY,
      ScreenX: event.screenX,
      ScreenY: event.screenY,
      PageX: event.pageX,
      PageY: event.pageY,
      MovementX: event.movementX,
      MovementY: event.movementY,
      Button: event.button,
      Buttons: event.buttons,
      ShiftKey: event.shiftKey,
      CtrlKey: event.ctrlKey,
      AltKey: event.altKey,
      DeltaX: event.deltaX,
      DeltaY: event.deltaY,
      DeltaZ: event.deltaZ,
      DeltaMode: event.deltaMode,
    });
  }

  document.getElementById(elementId).addEventListener(eventName, eventHandler)
}

export function addPointerEventListener(elementId, eventName, instance, functionName) {
  function eventHandler(event) {

    if (eventName == "pointerdown") {
      event.target.setPointerCapture(event.pointerId);
    }
    if (eventName == "pointerup") {
      event.target.releasePointerCapture(event.pointerId);
    }
    if (eventName == "pointercancel") {
      event.target.releasePointerCapture(event.pointerId);
    }

    instance.invokeMethodAsync(functionName, {
      Type: eventName,
      Time: Date.now(),
      PointerId: event.pointerId,
      PointerType: event.pointerType,
      TargetId: event.target.id,
      OffsetX: event.offsetX,
      OffsetY: event.offsetY,
      ClientX: event.clientX,
      ClientY: event.clientY,
      ScreenX: event.screenX,
      ScreenY: event.screenY,
      PageX: event.pageX,
      PageY: event.pageY,
      MovementX: event.movementX,
      MovementY: event.movementY,
      Button: event.button,
      Buttons: event.buttons,
      ShiftKey: event.shiftKey,
      CtrlKey: event.ctrlKey,
      AltKey: event.altKey,
      DeltaX: event.deltaX,
      DeltaY: event.deltaY,
      DeltaZ: event.deltaZ,
      DeltaMode: event.deltaMode,
    });
  }

  document.getElementById(elementId).addEventListener(eventName, eventHandler)
}

const openDatabases = new Map();

function formatDatabaseError(prefix, error) {
  const detail = error?.message ?? "unknown error";
  return new Error(`${prefix}: ${detail}`);
}

function openDatabase(databaseName, version, onUpgradeNeeded) {
  return new Promise((resolve, reject) => {
    const request = version == null ? indexedDB.open(databaseName) : indexedDB.open(databaseName, version);
    let isSettled = false;

    request.onupgradeneeded = (event) => {
      try {
        if (onUpgradeNeeded) {
          onUpgradeNeeded(request.result, event);
        }
      } catch (error) {
        isSettled = true;
        reject(formatDatabaseError(`Failed upgrading database '${databaseName}'`, error));
      }
    };

    request.onsuccess = () => {
      if (isSettled) {
        request.result.close();
        return;
      }

      const db = request.result;
      db.onversionchange = () => {
        db.close();
        openDatabases.delete(databaseName);
      };
      resolve(db);
    };

    request.onerror = () => {
      isSettled = true;
      reject(formatDatabaseError(`Failed opening database '${databaseName}'`, request.error));
    };

    request.onblocked = () => {
      isSettled = true;
      reject(new Error(`Opening database '${databaseName}' was blocked by another open connection`));
    };
  });
}

async function getDatabase(databaseName) {
  let databasePromise = openDatabases.get(databaseName);
  if (!databasePromise) {
    databasePromise = openDatabase(databaseName);
    openDatabases.set(databaseName, databasePromise);
  }

  try {
    return await databasePromise;
  } catch (error) {
    openDatabases.delete(databaseName);
    throw error;
  }
}

function awaitRequest(request, description) {
  return new Promise((resolve, reject) => {
    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(formatDatabaseError(description, request.error));
  });
}

function awaitTransaction(transaction, description) {
  return new Promise((resolve, reject) => {
    transaction.oncomplete = () => resolve();
    transaction.onerror = () => reject(formatDatabaseError(description, transaction.error));
    transaction.onabort = () => reject(formatDatabaseError(description, transaction.error));
  });
}

export async function initializeDatabase(databaseName, currentVersion, collectionNames) {
  const existingDatabasePromise = openDatabases.get(databaseName);
  if (existingDatabasePromise) {
    try {
      const existingDatabase = await existingDatabasePromise;
      existingDatabase.close();
    } catch (_) {
      // Ignore and continue with a fresh open below.
    } finally {
      openDatabases.delete(databaseName);
    }
  }

  const database = await openDatabase(databaseName, currentVersion, (db) => {
    collectionNames.forEach(collectionName => {
      if (!db.objectStoreNames.contains(collectionName)) {
        db.createObjectStore(collectionName, { keyPath: "id" });
      }
    });
  });

  openDatabases.set(databaseName, Promise.resolve(database));
}

export async function setDatabaseValue(databaseName, collectionName, value) {
  const db = await getDatabase(databaseName);
  const transaction = db.transaction(collectionName, "readwrite");
  const collection = transaction.objectStore(collectionName);
  const transactionCompletion = awaitTransaction(transaction, `Failed committing write to ${databaseName}.${collectionName}`);

  await awaitRequest(collection.put(value), `Failed writing value to ${databaseName}.${collectionName}`);
  await transactionCompletion;
}

async function getDatabaseValueText(databaseName, collectionName, id) {
  const db = await getDatabase(databaseName);
  const transaction = db.transaction(collectionName, "readonly");
  const collection = transaction.objectStore(collectionName);
  const value = await awaitRequest(
    collection.get(id),
    `Failed reading value ${databaseName}.${collectionName}.${id}`
  );

  if (value == null) {
    return null;
  }

  return JSON.stringify(value);
}

export async function getDatabaseValueStream(databaseName, collectionName, id) {
  const text = await getDatabaseValueText(databaseName, collectionName, id);
  if (text == null) {
    return null;
  }

  // When the call result type is IJSStreamReference, Blazor can wrap typed arrays/blobs as stream references.
  return new TextEncoder().encode(text);
}

export async function deleteDatabaseValue(databaseName, collectionName, id) {
  const db = await getDatabase(databaseName);
  const transaction = db.transaction(collectionName, "readwrite");
  const collection = transaction.objectStore(collectionName);
  const transactionCompletion = awaitTransaction(
    transaction,
    `Failed committing delete for ${databaseName}.${collectionName}.${id}`
  );

  await awaitRequest(collection.delete(id), `Failed deleting value ${databaseName}.${collectionName}.${id}`);
  await transactionCompletion;
}

export async function getDatabaseAllKeys(databaseName, collectionName) {
  const db = await getDatabase(databaseName);
  const transaction = db.transaction([collectionName], "readonly");
  const objectStore = transaction.objectStore(collectionName);

  const keys = await awaitRequest(objectStore.getAllKeys(), `Failed reading keys for ${databaseName}.${collectionName}`);
  return keys;
}

// --- Clerk authentication interop ---

// Waits for the Clerk global to exist and be fully loaded (up to 15 seconds).
// The Clerk CDN script sets window.Clerk but it may not be .loaded yet.
function waitForClerk(timeoutMs = 15000) {
  return new Promise((resolve, reject) => {
    function check() {
      const c = window.Clerk;
      if (c && c.loaded) return c;
      // Clerk object exists but hasn't finished initializing yet
      if (c && typeof c.load === "function") return null; // needs .load()
      return null;
    }

    const ready = check();
    if (ready) {
      console.log("DEP: Clerk already loaded");
      resolve(ready);
      return;
    }

    const start = Date.now();
    const interval = setInterval(async () => {
      // If Clerk object exists but isn't loaded, try calling .load()
      const c = window.Clerk;
      if (c && !c.loaded && typeof c.load === "function") {
        clearInterval(interval);
        console.log("DEP: Clerk object found, calling .load()...");
        try {
          await c.load();
          console.log("DEP: Clerk.load() completed, loaded:", c.loaded);
          resolve(c);
        } catch (err) {
          console.error("DEP: Clerk.load() failed:", err);
          reject(new Error("Clerk.load() failed: " + err.message));
        }
        return;
      }

      if (c && c.loaded) {
        clearInterval(interval);
        console.log("DEP: Clerk loaded via polling");
        resolve(c);
        return;
      }

      if (Date.now() - start > timeoutMs) {
        clearInterval(interval);
        console.error("DEP: Clerk.js did not load in time. window.Clerk =", window.Clerk);
        reject(new Error("Clerk.js did not load in time."));
      }
    }, 200);
  });
}

export async function clerkIsLoaded() {
  const clerk = window.Clerk;
  return clerk !== null && clerk !== undefined && clerk.loaded === true;
}

export async function clerkGetToken() {
  try {
    const clerk = await waitForClerk();
    if (!clerk.session) {
      console.log("DEP: clerkGetToken - no session");
      return null;
    }
    const token = await clerk.session.getToken({ template: 'dependinator' });
    console.log("DEP: clerkGetToken - got token:", !!token);
    return token;
  } catch (err) {
    console.error("DEP: clerkGetToken failed:", err);
    return null;
  }
}

// Opens Clerk sign-in modal and waits for sign-in to complete (up to 5 minutes).
// Magic links open a new tab; the new tab detects the localStorage flag, shows a
// minimal page, and broadcasts sign-in completion. This tab reloads to pick up the session.
export async function clerkSignIn() {
  console.log("DEP: clerkSignIn called (modal)");
  const clerk = await waitForClerk();
  if (clerk.user) return true;

  // Set flag so the magic-link redirect tab shows a minimal page instead of full WASM
  localStorage.setItem("dep-clerk-signin", Date.now().toString());

  return new Promise((resolve) => {
    let resolved = false;

    function complete(success, reload) {
      if (resolved) return;
      resolved = true;
      clearInterval(pollInterval);
      try { bc.close(); } catch (_) { }
      try { clerk.closeSignIn(); } catch (_) { }
      localStorage.removeItem("dep-clerk-signin");
      console.log("DEP: clerkSignIn resolved:", success, "reload:", !!reload);
      if (reload) {
        window.location.reload();
      } else {
        resolve(success);
      }
    }

    // Listen for broadcast from the magic-link redirect tab
    const bc = new BroadcastChannel("dep-clerk-auth");
    bc.onmessage = (event) => {
      if (event.data?.type === "signed-in") {
        console.log("DEP: Sign-in detected via BroadcastChannel");
        complete(true, true);
      }
    };

    // Also poll clerk.user as fallback (e.g. if sign-in completes in same tab)
    const pollInterval = setInterval(() => {
      if (clerk.user) {
        console.log("DEP: Sign-in detected via polling");
        complete(true, false);
      }
    }, 1000);

    // Open the modal sign-in UI
    clerk.openSignIn({});

    // Timeout after 5 minutes
    setTimeout(() => {
      console.log("DEP: Clerk sign-in timed out");
      complete(false, false);
    }, 300000);
  });
}

export async function clerkSignOut() {
  console.log("DEP: clerkSignOut called");
  const clerk = await waitForClerk();
  await clerk.signOut();
  return true;
}

export async function clerkIsAuthenticated() {
  try {
    const clerk = await waitForClerk();
    return !!clerk.user;
  } catch {
    return false;
  }
}

// --- End Clerk authentication interop ---

export function initializeFileDropZone(dropZoneElement, inputFileElement) {
  function onDragHover(e) {
    e.preventDefault();
    dropZoneElement.classList.add("hover");
  }

  function onDragLeave(e) {
    e.preventDefault();
    dropZoneElement.classList.remove("hover");
  }


  // Handle the paste and drop events
  function onDrop(e) {
    e.preventDefault();
    dropZoneElement.classList.remove("hover");

    // Set the files property of the input element and raise the change event
    inputFileElement.files = e.dataTransfer.files;
    const event = new Event('change', { bubbles: true });
    inputFileElement.dispatchEvent(event);
  }

  function onPaste(e) {
    // Set the files property of the input element and raise the change event
    inputFileElement.files = e.clipboardData.files;
    const event = new Event('change', { bubbles: true });
    inputFileElement.dispatchEvent(event);
  }

  dropZoneElement.addEventListener("dragenter", onDragHover);
  dropZoneElement.addEventListener("dragover", onDragHover);
  dropZoneElement.addEventListener("dragleave", onDragLeave);
  dropZoneElement.addEventListener("drop", onDrop);
  dropZoneElement.addEventListener('paste', onPaste);
}
