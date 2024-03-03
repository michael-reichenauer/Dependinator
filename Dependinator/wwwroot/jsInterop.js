export function showPrompt(message) {
  return prompt(message, 'Type anything here');
}

export function listenToWindowResize(dotNetHelper) {
  function resizeEventHandler() {
    dotNetHelper.invokeMethodAsync('WindowResizeEvent');
  }

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

    instance.invokeMethodAsync(functionName, {
      Type: eventName,
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

