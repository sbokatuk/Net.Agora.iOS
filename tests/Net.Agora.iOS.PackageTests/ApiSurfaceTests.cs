using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Net.Agora.iOS.PackageTests;

/// <summary>
/// Asserts the ergonomic API surface the bindings promise — the [Async] methods, the generated
/// events and the constructors that must NOT exist — by reading the binding assembly's metadata
/// straight out of the packed .nupkg. Like every other test here it runs against the package, not
/// the build output, and it deliberately never loads the assembly: a net9.0 test process cannot
/// resolve Microsoft.iOS references, but metadata inspection needs none of them.
/// </summary>
/// <remarks>
/// These exist because the surface is generated: an attribute lost in an ApiDefinition.cs edit
/// (an [Async] dropped, an [EventArgs] renamed, a [DisableDefaultCtor] removed) still builds
/// perfectly and ships a package with the sugar silently gone — or, for the constructor, silently
/// back. The generator's output is the contract, so the contract is what gets asserted.
/// </remarks>
public class ApiSurfaceTests
{
    /// <summary>
    /// The surface is identical across target frameworks — same ApiDefinition.cs, same generator —
    /// so these read one assembly rather than three, same reasoning as
    /// <c>PackageLayoutTests.PayloadTargetFramework</c>.
    /// </summary>
    private const string SurfaceTargetFramework = "net9.0-ios18.0";

    [Fact]
    public void Signaling_client_exposes_the_awaitable_login()
    {
        using var metadata = OpenBindingAssembly(Packages.Signaling, out var reader);

        var client = TypeNamed(reader, "AgoraRtmClientKit");

        // Task<AgoraRtmOperationResult>, not Task: the result class carries the (Response,
        // ErrorInfo) pair because the Task completes rather than faults on an RTM error — the
        // SDK answers some successes with a non-nil errorInfo whose code is 0, so the error
        // must be data, not an exception. See the [Async] comment in the Signaling
        // ApiDefinition.cs; a faulting shape appearing here means that contract changed.
        var returnTypes = MethodsNamed(reader, client, "LoginAsync").Select(m => ReturnTypeOf(reader, m)).ToList();

        Assert.True(returnTypes.Count > 0, "AgoraRtmClientKit has no LoginAsync method.");
        Assert.Contains("Task<AgoraRtmOperationResult>", returnTypes);
    }

    [Theory]
    [InlineData(Packages.Video)]
    [InlineData(Packages.Voice)]
    public void Rtc_engine_exposes_events_and_a_typed_delegate(string packageId)
    {
        using var metadata = OpenBindingAssembly(packageId, out var reader);

        var engine = TypeNamed(reader, "AgoraRtcEngineKit");

        // DidJoinChannel and DidJoinedOfUid are the events every call UI starts from (own join
        // confirmed, first remote user arrives), so they stand in for the whole generated set:
        // events only exist at all if the [BaseType] Events/Delegates pair survived, and each
        // name only exists if its callback's [EventArgs] did. Both packages are asserted with
        // the same names on purpose — the two engines are meant to feel identical.
        var events = reader.GetTypeDefinition(engine).GetEvents()
            .Select(handle => reader.GetString(reader.GetEventDefinition(handle).Name))
            .ToList();

        Assert.Contains("DidJoinChannel", events);
        Assert.Contains("DidJoinedOfUid", events);

        // The typed Delegate property is the other half of the pattern — the escape hatch for
        // consumers who bring an AgoraRtcEngineDelegate subclass instead of events.
        var properties = reader.GetTypeDefinition(engine).GetProperties()
            .Select(handle => reader.GetString(reader.GetPropertyDefinition(handle).Name))
            .ToList();

        Assert.Contains("Delegate", properties);
    }

    [Theory]
    [InlineData(Packages.Video)]
    [InlineData(Packages.Voice)]
    public void Rtc_engine_cannot_be_newed_up(string packageId)
    {
        using var metadata = OpenBindingAssembly(packageId, out var reader);

        var engine = TypeNamed(reader, "AgoraRtcEngineKit");

        // The engine only exists through SharedEngine. Before [DisableDefaultCtor],
        // `new AgoraRtcEngineKit()` compiled and handed back a broken non-shared instance —
        // an intentionally breaking fix, so its absence is asserted lest a refactor quietly
        // brings the trap back. Non-public constructors (the runtime's NativeHandle plumbing)
        // are expected and fine; what must not exist is a *public parameterless* one.
        var publicParameterless = reader.GetTypeDefinition(engine).GetMethods()
            .Select(reader.GetMethodDefinition)
            .Where(method => reader.GetString(method.Name) == ".ctor")
            .Where(method => (method.Attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Public)
            .Where(method => ParameterCountOf(reader, method) == 0)
            .ToList();

        Assert.True(
            publicParameterless.Count == 0,
            $"{packageId}'s AgoraRtcEngineKit has a public parameterless constructor again.");
    }

    /// <summary>
    /// Opens the binding assembly inside a package for metadata inspection. The returned reader
    /// is only valid while the PEReader lives, hence returning it for disposal.
    /// </summary>
    private static PEReader OpenBindingAssembly(string packageId, out MetadataReader reader)
    {
        using var package = Packages.OpenPackage(packageId);

        var pe = new PEReader(Packages.ReadEntry(package, $"lib/{SurfaceTargetFramework}/{packageId}.dll"));
        reader = pe.GetMetadataReader();
        return pe;
    }

    private static TypeDefinitionHandle TypeNamed(MetadataReader reader, string name)
    {
        foreach (var handle in reader.TypeDefinitions)
        {
            if (reader.GetString(reader.GetTypeDefinition(handle).Name) == name)
            {
                return handle;
            }
        }

        Assert.Fail($"The assembly defines no type named '{name}'.");
        return default;
    }

    private static IEnumerable<MethodDefinition> MethodsNamed(
        MetadataReader reader, TypeDefinitionHandle type, string name) =>
        reader.GetTypeDefinition(type).GetMethods()
            .Select(reader.GetMethodDefinition)
            .Where(method => reader.GetString(method.Name) == name);

    private static string ReturnTypeOf(MetadataReader reader, MethodDefinition method) =>
        method.DecodeSignature(TypeNameDecoder.Instance, null).ReturnType;

    private static int ParameterCountOf(MetadataReader reader, MethodDefinition method) =>
        method.DecodeSignature(TypeNameDecoder.Instance, null).ParameterTypes.Length;

    /// <summary>
    /// Renders decoded signature types as bare names ("Task&lt;AgoraRtmOperationResult&gt;"),
    /// which is all these assertions compare. Namespaces are deliberately dropped: the assertion
    /// is about the generated shape, and the short names are unambiguous within it.
    /// </summary>
    private sealed class TypeNameDecoder : ISignatureTypeProvider<string, object?>
    {
        public static readonly TypeNameDecoder Instance = new();

        public string GetPrimitiveType(PrimitiveTypeCode typeCode) => typeCode.ToString();

        public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind) =>
            reader.GetString(reader.GetTypeDefinition(handle).Name);

        public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind) =>
            reader.GetString(reader.GetTypeReference(handle).Name);

        public string GetTypeFromSpecification(MetadataReader reader, object? genericContext, TypeSpecificationHandle handle, byte rawTypeKind) =>
            reader.GetTypeSpecification(handle).DecodeSignature(this, genericContext);

        public string GetGenericInstantiation(string genericType, System.Collections.Immutable.ImmutableArray<string> typeArguments) =>
            $"{StripArity(genericType)}<{string.Join(",", typeArguments)}>";

        public string GetSZArrayType(string elementType) => $"{elementType}[]";

        public string GetArrayType(string elementType, ArrayShape shape) => $"{elementType}[{new string(',', shape.Rank - 1)}]";

        public string GetByReferenceType(string elementType) => $"{elementType}&";

        public string GetPointerType(string elementType) => $"{elementType}*";

        public string GetFunctionPointerType(MethodSignature<string> signature) => "fnptr";

        public string GetGenericMethodParameter(object? genericContext, int index) => $"!!{index}";

        public string GetGenericTypeParameter(object? genericContext, int index) => $"!{index}";

        public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired) => unmodifiedType;

        public string GetPinnedType(string elementType) => elementType;

        /// <summary>Task`1 → Task: metadata names carry the arity suffix, assertions should not.</summary>
        private static string StripArity(string name)
        {
            var backtick = name.IndexOf('`');
            return backtick < 0 ? name : name[..backtick];
        }
    }
}
