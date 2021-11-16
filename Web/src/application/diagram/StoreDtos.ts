import { Box } from "./draw2dTypes";

export interface FigureDto {
  y: any;
  x: any;
  type: string;
}
export interface ConnectionDto {}

export interface CanvasDto {
  diagramId: string;
  diagramName: string;
  canvasId: string;
  mainNodeId: string;
  box: Box;
  figures: FigureDto[];
  connections: ConnectionDto[];
  zoom: number;
}

export interface SyncDto {}
export interface DiagramInfoDto {
  diagramId: string;
  accessed: number;
  written: number;
}
export interface DiagramDto {
  diagramInfo: DiagramInfoDto;
  canvases: CanvasDto[];
}

export interface Dto {}
