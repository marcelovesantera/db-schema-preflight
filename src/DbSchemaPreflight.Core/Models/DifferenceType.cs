namespace DbSchemaPreflight.Core.Models;

public enum DifferenceType
{
    MissingTable,
    ExtraTable,
    MissingColumn,
    ExtraColumn,
    DataTypeMismatch,
    DataLengthSmaller,
    DataLengthLarger,
    PrecisionMismatch,
    ScaleMismatch,
    NullabilityMismatch,
    DefaultValueMismatch
}
