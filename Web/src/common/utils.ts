// Some handy utility functions

const humanizeDuration = require("humanize-duration");

// Returns a duration as a nice human readable string
export const durationString = (duration: number): string => {
  return humanizeDuration(duration);
};

// Returns a random number between min and max
export const random = (min: number, max: number): number => {
  min = Math.ceil(min);
  max = Math.floor(max) + 1;
  return Math.floor(Math.random() * (max - min) + min);
};

// Returns the distance between 2 points
export const distance = (
  x1: number,
  y1: number,
  x2: number,
  y2: number
): number => {
  return Math.sqrt((x1 - x2) * (x1 - x2) + (y1 - y2) * (y1 - y2));
};

// Async sleep/delay
export async function delay(time: number): Promise<void> {
  return new Promise((res) => {
    setTimeout(res, time);
  });
}

// Returns the sha 256 hash of the string
export async function sha256Hash(text: string) {
  // encode as UTF-8
  const msgBuffer = new TextEncoder().encode(text);

  // hash the message
  const hashBuffer = await crypto.subtle.digest("SHA-256", msgBuffer);

  // convert ArrayBuffer to Array
  const hashArray = Array.from(new Uint8Array(hashBuffer));

  // convert bytes to hex string
  const hashHex = hashArray
    .map((b) => b.toString(16).padStart(2, "0"))
    .join("");
  return hashHex;
}

// Returns if build is developer mode (running on local machine)
export const isDeveloperMode: boolean =
  !process.env.NODE_ENV || process.env.NODE_ENV === "development";

export const isLocal: boolean =
  window.location.hostname === "localhost" ||
  window.location.hostname === "127.0.0.1";

export const fetchFiles = (
  paths: string[],
  result: (files: string[]) => void
): void => {
  Promise.all(paths.map((path) => fetch(path)))
    .then((responses) => {
      // Get the file for each response
      return Promise.all(
        responses.map((response) => {
          return response.text();
        })
      );
    })
    .then((files) => {
      result(files);
    })
    .catch((error) => {
      // if there's an error, log it
      result([]);
      console.log(error);
    });
};

export const svgToSvgDataUrl = (svg: string): string => {
  return "data:image/svg+xml;charset=utf-8," + encodeURIComponent(svg);
};

export const publishAsDownload = (dataUrl: string, name: string) => {
  var link = document.createElement("a");
  link.download = name;
  link.style.opacity = "0";
  document.body.append(link);
  link.href = dataUrl;
  link.click();
  link.remove();
};

export const imgDataUrlToPngDataUrl = (
  imgDataUrl: string,
  width: number,
  height: number,
  result: (url: string) => void
) => {
  const image = new Image();
  image.onload = () => {
    const canvas = document.createElement("canvas");
    canvas.width = width;
    canvas.height = height;

    const context = canvas.getContext("2d");
    context?.drawImage(image, 0, 0, canvas.width, canvas.height);

    const pngDataUrl = canvas.toDataURL(); // default png

    result(pngDataUrl);
  };

  image.src = imgDataUrl;
};
