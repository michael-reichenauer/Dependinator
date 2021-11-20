import { Box } from "./draw2dTypes";

export interface FigureDto {
  icon: string | undefined;
  type: string | undefined;
  color: string;
  description: string;
  name: string;
  x: number;

  y: number;
  h: number;
  w: number;
  id: string;

  hasGroup: boolean;
}
export interface VertexDto {
  x: number;
  y: number;
}

export interface ConnectionDto {
  id: string;
  src: string;
  srcPort: string;
  srcGrp: boolean;
  trg: string;
  trgPort: string;
  trgGrp: boolean;
  v: VertexDto[];
  name: string;
  description: string;
}

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
