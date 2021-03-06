﻿module Bit.Contracts {
    export interface ISecurityService {
        isLoggedIn(): boolean;
        login(state?: any): void;
        logout(): void;
        loginWithCredentials(username: string, password: string, client_id: string, client_secret: string, scopes: string[], saveToken: boolean): Promise<{ access_token: string, expires_in: number, token_type: string }>;
    }
}