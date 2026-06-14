// Shared cross-layer fixture: backend tests read this file and verify it still
// matches ChatStreamEventContract.ToSseDataLine output.
export const PUBLIC_CHAT_HAPPY_PATH_SSE = String.raw`data: {"schemaVersion":1,"type":"sources","fallback":{"used":false},"sources":[{"id":"part-1","catalogItemId":"ci-1","code":"4109410","name":"Ya\u011F deposu contas\u0131","pageNumber":"12","similarity":0.91}]}

data: {"schemaVersion":1,"type":"token","fallback":{"used":false},"token":"Par\u00E7a "}

data: {"schemaVersion":1,"type":"token","fallback":{"used":false},"token":"4109410 bulundu."}

data: {"schemaVersion":1,"type":"done","fallback":{"used":false},"completion":{"status":"completed"}}

`;
