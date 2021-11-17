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

export interface SyncDto {
  isConnected: boolean;
  token: string | null;
  isConnecting: boolean;
  provider: string | null;
  details: string | null;
}

export interface DiagramInfoDto {
  etag: string;
  timestamp: number;
  name: string;
  diagramId: string;
  accessed: number;
  written: number;
}

export interface DiagramDto {
  diagramInfo: DiagramInfoDto;
  canvases: CanvasDto[];
}

export interface Dto {}
