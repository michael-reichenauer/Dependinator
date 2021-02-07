
import React from "react";
import Canvas from "./canvas/Canvas";



export default function Diagram({ width, height, commands }) {

    console.log
    return (
        <>
            <Canvas width={width} height={height} commands={commands} />
        </>
    )
}