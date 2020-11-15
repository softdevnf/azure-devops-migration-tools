# Work item post processing

> Note: I don't know what this does 🤷 It changes work items like `WorkItemMigrationConfig` but without that cool options. 



| Parameter name | Type       | Description                              | Default Value                            |
|----------------|------------|------------------------------------------|------------------------------------------|
| `Enabled`      | Boolean    | Active the processor if it true.         | false                                    |
| `$type`   | string     | The name of the processor                | WorkItemPostProcessingConfig |
| `QueryBit`     | string     |  A work item query to select only important work items. To migrate all leave this empty. | false                                    |
| `WorkItemIDs`  | Array<int> | Define a list of work item ids. If you use this with the `QueryBit` parameter that both parameters must return in a `true` to get changed.                                     |
