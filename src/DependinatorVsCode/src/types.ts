export type WebviewMessage = {
    type: string;
    message?: unknown;
};

export type CloudSyncEnvelope = {
    RequestId: string;
    Action: string;
    Payload?: string | null;
    Error?: string | null;
};
