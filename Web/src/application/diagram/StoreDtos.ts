// export interface SyncDto {
//   isConnected: boolean;
//   token: string | null;
//   isConnecting: boolean;
//   provider: string | null;
//   details: string | null;
// }

export const applicationKey = "application";

export type DiagramInfoDtos = { [id: string]: DiagramInfoDto };

export type CanvasDtos = { [id: string]: CanvasDto };

export interface ApplicationDto {
  diagramInfos: DiagramInfoDtos;
}

export interface DiagramInfoDto {
  id: string;
  name: string;
  accessed: number;
}

export interface DiagramDto {
  id: string;
  name: string;
  canvases: CanvasDtos;
}

export interface CanvasDto {
  id: string;
  rect: RectDto;
  figures: FigureDto[];
  connections: ConnectionDto[];
}

export interface FigureDto {
  id: string;
  type: string | undefined;
  name: string;
  description: string;
  rect: RectDto;
  color: string;
  icon?: string;
  sticky?: boolean;
}

export interface VertexDto {
  x: number;
  y: number;
}

export interface RectDto {
  x: number;
  y: number;
  w: number;
  h: number;
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

export interface FileDto {
  diagrams: DiagramDto[];
}
