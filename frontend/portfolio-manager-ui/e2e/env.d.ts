/** Minimal typing for the env vars the harness reads; avoids pulling @types/node into the Angular build. */
declare const process: { env: Record<string, string | undefined> };
