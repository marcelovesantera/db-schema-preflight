namespace DbSchemaPreflight.Core.ScriptAnalysis.Models;

public enum StatementType
{
    Insert,
    InsertSelect,
    Update,
    Delete,
    CreateTable,
    AlterTableAdd,
    CreateOrReplaceView,
    PlSqlBlock,
    Other
}
