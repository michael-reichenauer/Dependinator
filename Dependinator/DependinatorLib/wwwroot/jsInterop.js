export function showPrompt(message) {
  return prompt(message, 'Type anything here');
}

export function getBoundingRectangle(elementId, parm) {
  var element = document.getElementById(elementId);
  return element.getBoundingClientRect();
}