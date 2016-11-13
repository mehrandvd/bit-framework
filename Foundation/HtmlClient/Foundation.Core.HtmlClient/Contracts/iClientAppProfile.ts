﻿module Foundation.Core.Contracts {

    export interface EnvironmentConfig {
        key: string;
        value: any;
    }

    export interface IClientAppProfile {
        screenSize: "DesktopAndTablet" | "MobileAndPhablet" | "";
        theme: string;
        culture: string;
        version: string;
        isDebugMode: boolean;
        clientType: "Device" | "Web" | "";
        calendar: string;
        direction: "Ltr" | "Rtl";
        appTitle: string;
        appName: string;
        currentTimeZone: string;
        desiredTimeZone: string;
        environmentConfigs: Array<EnvironmentConfig>;
        getConfig<T>(configKey: string, defaultValueOnNotFound?: T): T;
    }
}