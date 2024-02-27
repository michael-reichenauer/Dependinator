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

export function addTouchEventListener(elementId, eventName, instance, functionName) {
  function eventHandler(event) {

    event.preventDefault();
    event.stopPropagation();
    event.stopImmediatePropagation();

    let touches = Array.from(event.touches, touch => ({
      Identifier: touch.identifier,
      TargetId: touch.target.id,
      ClientX: touch.clientX,
      ClientY: touch.clientY,
      ScreenX: touch.screenX,
      ScreenY: touch.screenY,
      PageX: touch.pageX,
      PageY: touch.pageY,
    }));

    instance.invokeMethodAsync(functionName, {
      Type: eventName,
      TargetId: event.target.id,
      Touches: touches,
      CtrlKey: event.ctrlKey,
      ShiftKey: event.shiftKey,
      AltKey: event.altKey,
      MetaKey: event.metaKey,
    });
  }

  window.addEventListener(eventName, eventHandler, { passive: false })
}

export function addHammerListener(elementId, instance, functionName) {
  var element = document.getElementById(elementId);
  var hammer = new Hammer(element);

  // Enable pinch and pan
  hammer.get('pinch').set({ enable: true });
  hammer.get('pan').set({ direction: Hammer.DIRECTION_ALL });

  // Listen for pan start, pan move, and pan end
  hammer.on("panstart panmove panend", function (ev) {
    instance.invokeMethodAsync(functionName, getEventDetails(ev));
  });

  // Listen for pinch in and out
  hammer.on("pinchmove pinchstart", function (ev) {
    instance.invokeMethodAsync(functionName, getEventDetails(ev));
  });

  // hammer.on('tap', function (ev) {
  //   instance.invokeMethodAsync(functionName, getEventDetails(ev));
  // });
}

function getEventDetails(ev) {
  return {
    Type: ev.type, TargetId: ev.target.id,
    Center: { X: ev.center.x, Y: ev.center.y },
    Pointers: ev.pointers.map(p => ({ X: p.clientX, Y: p.clientY })),
    DeltaX: ev.deltaX, DeltaY: ev.deltaY, VelocityX: ev.velocityX, VelocityY: ev.velocityY,
    Scale: ev.scale, Rotation: ev.rotation,
    IsFirst: ev.isFirst, IsFinal: ev.isFinal
  }
}

