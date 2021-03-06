﻿
module Bit.Implementations {
    export class DefaultKendoExtender implements Contracts.IAppEvents {
        @Log()
        public async onAppStartup(): Promise<void> {

            function flattenGroups(data) {

                let idx, result = [], length, items, itemIndex;

                for (idx = 0, length = data.length; idx < length; idx++) {
                    const group = data[idx];
                    if (group.hasSubgroups) {
                        result = result.concat(flattenGroups(group.items));
                    } else {
                        items = group.items;
                        for (itemIndex = 0; itemIndex < items.length; itemIndex++) {
                            result.push(items[itemIndex] /* instead of items.at(itemIndex) */);
                        }
                    }
                }

                return result;

            }

            kendo.data.DataSource.prototype.flatView = function () {

                const groups = this.group() || [];

                if (groups.length) {
                    return flattenGroups(this._view);
                } else {
                    return this._view;
                }

            }

            const originalParseDate = kendo.parseDate;

            kendo.parseDate = function (value: string, format?: string, culture?: string): Date {
                if (value != null) {
                    const date = new Date(value);
                    if (date.toString() != "Invalid Date")
                        return date;
                }
                return originalParseDate.apply(this, arguments);
            };

            kendo.data.DataSource.prototype.dataView = function () {
                return (this as kendo.data.DataSource)
                    .flatView()
                    .map(vi => {
                        const viItem = (vi as any);
                        return viItem.innerInstance != null ? viItem.innerInstance() : viItem;
                    });
            };

            Object.defineProperty(kendo.data.DataSource.prototype, "current", {
                configurable: false,
                enumerable: false,
                get: function () {
                    return this["_current"];
                },
                set: function (entity: $data.Entity) {

                    this["_current"] = entity;

                    if (this["setHandlers"] == null)
                        return;

                    for (let setHandler of this["setHandlers"])
                        setHandler(entity);

                    this.onCurrentChanged();
                }
            });

            kendo.data.DataSource.prototype.onCurrentChanged = function (action) {

                const dataSource = this;

                dataSource.onCurrentChangedHandlers = dataSource.onCurrentChangedHandlers || [];

                if (action != null) {
                    dataSource.onCurrentChangedHandlers.push(action);
                    return () => {
                        let index = dataSource.onCurrentChangedHandlers.indexOf(action);
                        if (index > -1)
                            dataSource.onCurrentChangedHandlers.splice(index, 1);
                    };
                } else {

                    for (let handler of dataSource.onCurrentChangedHandlers) {
                        handler();
                    }
                }

            };

            kendo.destroyWidget = function (widget: kendo.ui.Widget & { wrapper: JQuery }): void {

                if (widget != null) {

                    if (widget.wrapper != null) {

                        widget.wrapper.each(function (id, kElement) {
                            const dataObj = angular.element(kElement).data();
                            for (let mData in dataObj) {
                                if (dataObj.hasOwnProperty(mData)) {
                                    if (angular.isObject(dataObj[mData])) {
                                        if (typeof dataObj[mData]["destroy"] == "function") {
                                            dataObj[mData].destroy();
                                        }
                                    }
                                }
                            }
                        });

                        widget.wrapper.remove();
                    }

                    widget.destroy();
                }
            }

            kendo.data.DataSource.prototype.asChildOf = function (parentDataSource, childKeys, parentKeys) {

                if (parentDataSource == null)
                    throw new Error("parentDataSource is null");

                if (childKeys == null || childKeys.length == 0) {
                    throw new Error("childs keys is null or empty");
                }

                if (parentKeys == null || parentKeys.length == 0) {
                    throw new Error("parent keys is null or empty");
                }

                if (childKeys.length != parentKeys.length) {
                    throw new Error("Child keys and parent keys must have the same length");
                }

                const childDataSource: kendo.data.DataSource = this;

                for (let key of childKeys) {
                    if (childDataSource.options.schema.model.fields[key] == null) {
                        throw new Error(`child data source schema has no property named ${key}`);
                    }
                }

                for (let key of parentKeys) {
                    if (parentDataSource.options.schema.model.fields[key] == null) {
                        throw new Error(`parent data source schema has no property named ${key}`);
                    }
                }

                parentDataSource.onCurrentChanged(async () => {

                    const currentParent = parentDataSource.current;

                    await new Promise<void>((resolve) => {
                        setTimeout(() => resolve(), 350);
                    });

                    if (currentParent != parentDataSource.current)
                        return;

                    if (childDataSource.current != null) {
                        try {
                            let currentChild = childDataSource.current;
                            if (currentParent == null || parentKeys.some((pk, i) => currentChild[childKeys[i]] != currentParent[pk]))
                                childDataSource.current = null;
                        }
                        catch (e) { }
                    }

                    await childDataSource.read();
                });

                const originalChildTransportRead = childDataSource["transport"].read;

                childDataSource["transport"].read = function (options) {

                    const currentParent = parentDataSource.current;

                    if (currentParent == null || parentKeys.some(pk => currentParent[pk] == null)) {
                        options.success({ data: [], length: 0 });
                    }
                    else {

                        options.data = options.data || {};
                        options.data.cascadeBaseFilter = childKeys.map((childKey, index) => {
                            let parentVal = currentParent[parentKeys[index]];
                            if (typeof parentVal == "string")
                                parentVal = "'" + parentVal + "'";
                            return `it.${childKey} == ${parentVal}`;
                        }).join("&&");

                        return originalChildTransportRead.apply(this, arguments);
                    }
                }

                const originalChildTransportCreate = childDataSource["transport"].create;

                childDataSource["transport"].create = function (options, models): void {

                    const currentParent = parentDataSource.current;

                    if (currentParent == null || parentKeys.some(pk => currentParent[pk] == null)) {
                        throw new Error("Parent data source's current item is null or new");
                    }

                    for (let model of models) {
                        for (let i = 0; i < childKeys.length; i++) {
                            model[childKeys[i]] = currentParent[parentKeys[i]];
                            (model.innerInstance != null ? model.innerInstance() : model)[childKeys[i]] = currentParent[parentKeys[i]];
                        }
                    }

                    return originalChildTransportCreate.apply(this, arguments);
                }
            }
        }
    }
}