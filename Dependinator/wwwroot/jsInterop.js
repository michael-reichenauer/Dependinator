export function showPrompt(message) {
  return prompt(message, 'Type anything here');
}

export function listenToWindowResize(dotNetHelper) {
  function resizeEventHandler() {
    dotNetHelper.invokeMethodAsync('WindowResizeEvent');
  }

  // Prevent default touch events(scrolling, zooming, etc.), handled by the app
  const elementId = "svgcanvas";
  document.getElementById(elementId).addEventListener('touchstart', function (e) {
    e.preventDefault();
  }, { passive: false });
  document.getElementById(elementId).addEventListener('touchmove', function (e) {
    e.preventDefault();
  }, { passive: false });
  document.getElementById(elementId).addEventListener('touchend', function (e) {
    e.preventDefault();
  }, { passive: false });

  window.addEventListener("resize", resizeEventHandler);

  dotNetHelper.invokeMethodAsync('WindowResizeEvent');
}

export function getBoundingRectangle(element, parm) {
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
    // console.log("mouse", event);
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


export function initializeDatabase(databaseName, currentVersion, collectionName) {
  const db = indexedDB.open(databaseName, currentVersion);

  db.onupgradeneeded = function () {
    db.result.createObjectStore(collectionName, { keyPath: "id" });
  }
}

export async function setDatabaseValue(databaseName, currentVersion, collectionName, value) {
  let request = new Promise((resolve) => {
    const db = indexedDB.open(databaseName, currentVersion);

    db.onsuccess = function () {
      const transaction = db.result.transaction(collectionName, "readwrite");
      const collection = transaction.objectStore(collectionName)

      const result = collection.put(value);
      result.onsuccess = function () {
        resolve(true);
      }
    }
  });

  await request;
}

export async function getDatabaseValue(databaseName, currentVersion, collectionName, id, instance, functionName) {
  let request = new Promise((resolve) => {
    const db = indexedDB.open(databaseName, currentVersion);
    db.onsuccess = function () {
      const transaction = db.result.transaction(collectionName, "readonly");
      const collection = transaction.objectStore(collectionName);
      const result = collection.get(id);

      result.onsuccess = function (e) {
        const value = result.result;

        if (value == null) {
          resolve(false);
          return;
        }

        // The value needs to sent as chunks if larger than packet size limit, which seems to be 30k
        const chunkSize = 30000;
        const text = JSON.stringify(value);
        var count = 0;
        for (let i = 0; i < text.length; i += chunkSize) {
          const chunk = text.substring(i, i + chunkSize);
          instance.invokeMethodAsync(functionName, chunk);
          count++;
        }

        resolve(true);
      }
    }
  });

  return await request;
}

export async function deleteDatabaseValue(databaseName, currentVersion, collectionName, id) {
  let request = new Promise((resolve) => {
    let db = indexedDB.open(databaseName, currentVersion);

    db.onsuccess = function () {
      let transaction = db.result.transaction(collectionName, "readwrite");
      let collection = transaction.objectStore(collectionName);

      const result = collection.delete(id);
      result.onsuccess = function () {
        resolve(true);
      }
    }
  });

  await request;
}

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

export function clickElement(element) {
  element.click();
}