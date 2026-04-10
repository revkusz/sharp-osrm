namespace Sharposrm.Route;

/// <summary>
/// Metadata for a <see cref="LegAnnotation"/>, describing the datasources used.
/// See: https://project-osrm.org/docs/v5.24.0/api/#annotation-object
/// </summary>
public sealed class AnnotationMetadata
{
    /// <summary>
    /// Names of the datasources referenced by <see cref="LegAnnotation.Datasources"/> indices.
    /// Each entry corresponds to a unique datasource used in the annotation.
    /// </summary>
    public List<string>? DatasourceNames { get; set; }
}
