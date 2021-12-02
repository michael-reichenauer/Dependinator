import { Box } from "./draw2dTypes";

// export interface SyncDto {
//   isConnected: boolean;
//   token: string | null;
//   isConnecting: boolean;
//   provider: string | null;
//   details: string | null;
// }

export const applicationKey = "application";

export interface ApplicationDto {
  id: string;
  timestamp?: number;
  diagramInfos: DiagramInfoDto[];
}

export interface DiagramInfoDto {
  id: string;
  name: string;
  accessed: number;
  written: number;
}

export interface DiagramDto {
  id: string;
  timestamp?: number;
  diagramInfo: DiagramInfoDto;
  canvases: CanvasDto[];
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

export interface FigureDto {
  id: string;
  sticky?: boolean;
  icon?: string;
  type: string | undefined;
  color: string;
  description: string;
  name: string;
  x: number;
  y: number;
  w: number;
  h: number;

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

// export interface SyncDto {
//   isConnected: boolean;
//   token: string | null;
//   isConnecting: boolean;
//   provider: string | null;
//   details: string | null;
// }

export interface Dto {}
