// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using System.Collections;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using MonoGame.Framework.Utilities;

namespace Microsoft.Xna.Framework.Content
{
    /// <summary>
    /// Defines a manager that constructs and keeps track of <see cref="ContentTypeReader"/> objects.
    /// </summary>
    public sealed class ContentTypeReaderManager
    {
        private static readonly object _locker;

        private static readonly Dictionary<string, Func<ContentTypeReader>> BuiltInCreators = new Dictionary<string, Func<ContentTypeReader>>(StringComparer.Ordinal);

        private Dictionary<Type, ContentTypeReader> _contentReaders;

        private static readonly string _assemblyName;

        private static readonly bool _isRunningOnNetCore = typeof(object).Assembly.GetName().Name == "System.Private.CoreLib";

        static ContentTypeReaderManager()
        {
            _locker = new object();
            _assemblyName = ReflectionHelpers.GetAssembly(typeof(ContentTypeReaderManager)).FullName;

            // Built-in content readers are created directly, without reflection.
            RegisterBuiltIn(() => new AlphaTestEffectReader());
            RegisterBuiltIn(() => new ArrayReader<int>());
            RegisterBuiltIn(() => new ArrayReader<float>());
            RegisterBuiltIn(() => new ArrayReader<char>());
            RegisterBuiltIn(() => new ArrayReader<string>());
            RegisterBuiltIn(() => new ArrayReader<Point>());
            RegisterBuiltIn(() => new ArrayReader<Vector2>());
            RegisterBuiltIn(() => new ArrayReader<Vector3>());
            RegisterBuiltIn(() => new ArrayReader<Matrix>());
            RegisterBuiltIn(() => new ArrayReader<Rectangle>());
            RegisterBuiltIn(() => new ArrayReader<Color>());
            RegisterBuiltIn(() => new ArrayReader<StringReader>());
            RegisterBuiltIn(() => new BasicEffectReader());
            RegisterBuiltIn(() => new BooleanReader());
            RegisterBuiltIn(() => new BoundingBoxReader());
            RegisterBuiltIn(() => new BoundingFrustumReader());
            RegisterBuiltIn(() => new BoundingSphereReader());
            RegisterBuiltIn(() => new ByteReader());
            RegisterBuiltIn(() => new CharReader());
            RegisterBuiltIn(() => new ColorReader());
            RegisterBuiltIn(() => new CurveReader());
            RegisterBuiltIn(() => new DateTimeReader());
            RegisterBuiltIn(() => new DecimalReader());
            // DictionaryReader<TKey, TValue>
            RegisterBuiltIn(() => new DoubleReader());
            RegisterBuiltIn(() => new DualTextureEffectReader());
            RegisterBuiltIn(() => new EffectMaterialReader());
            RegisterBuiltIn(() => new EffectReader());
            RegisterBuiltIn(() => new EnumReader<Graphics.SpriteEffects>());
            RegisterBuiltIn(() => new EnumReader<Graphics.Blend>());
            RegisterBuiltIn(() => new EnvironmentMapEffectReader());
            RegisterBuiltIn(() => new ExternalReferenceReader());
            RegisterBuiltIn(() => new IndexBufferReader());
            RegisterBuiltIn(() => new Int16Reader());
            RegisterBuiltIn(() => new Int32Reader());
            RegisterBuiltIn(() => new Int64Reader());
            RegisterBuiltIn(() => new ListReader<int>());
            RegisterBuiltIn(() => new ListReader<float>());
            RegisterBuiltIn(() => new ListReader<char>());
            RegisterBuiltIn(() => new ListReader<string>());
            RegisterBuiltIn(() => new ListReader<Point>());
            RegisterBuiltIn(() => new ListReader<Vector2>());
            RegisterBuiltIn(() => new ListReader<Vector3>());
            RegisterBuiltIn(() => new ListReader<Matrix>());
            RegisterBuiltIn(() => new ListReader<Rectangle>());
            RegisterBuiltIn(() => new ListReader<Color>());
            RegisterBuiltIn(() => new ListReader<StringReader>());
            RegisterBuiltIn(() => new MatrixReader());
            RegisterBuiltIn(() => new ModelReader());
            // MultiArrayReader<T>
            RegisterBuiltIn(() => new NullableReader<Rectangle>());
            RegisterBuiltIn(() => new PlaneReader());
            RegisterBuiltIn(() => new PointReader());
            RegisterBuiltIn(() => new QuaternionReader());
            RegisterBuiltIn(() => new RayReader());
            RegisterBuiltIn(() => new RectangleReader());
            // ReflectiveReader<T>
            RegisterBuiltIn(() => new SByteReader());
            RegisterBuiltIn(() => new SingleReader());
            RegisterBuiltIn(() => new SkinnedEffectReader());
            RegisterBuiltIn(() => new SongReader());
            RegisterBuiltIn(() => new SoundEffectReader());
            RegisterBuiltIn(() => new SpriteFontReader());
            RegisterBuiltIn(() => new StringReader());
            RegisterBuiltIn(() => new Texture2DReader());
            RegisterBuiltIn(() => new Texture3DReader());
            RegisterBuiltIn(() => new TextureCubeReader());
            RegisterBuiltIn(() => new TextureReader());
            RegisterBuiltIn(() => new TimeSpanReader());
            RegisterBuiltIn(() => new UInt16Reader());
            RegisterBuiltIn(() => new UInt32Reader());
            RegisterBuiltIn(() => new UInt64Reader());
            RegisterBuiltIn(() => new Vector2Reader());
            RegisterBuiltIn(() => new Vector3Reader());
            RegisterBuiltIn(() => new Vector4Reader());
            RegisterBuiltIn(() => new VertexBufferReader());
            RegisterBuiltIn(() => new VertexDeclarationReader());
            RegisterBuiltIn(() => new VideoReader());
        }

        /// <summary>
        /// Creates a new instance of the <see cref="ContentReader"/> class initialized for the specified type.
        /// </summary>
        /// <param name="targetType">The type the <see cref="ContentReader"/> will handle.</param>
        /// <returns>
        /// The <see cref="ContentReader"/> created by this method if a content reader of the specified type has been
        /// registered with this content manager; otherwise, null.
        /// </returns>
        /// <exception cref="ArgumentNullException">If the <paramref name="targetType"/> parameter is null.</exception>
        public ContentTypeReader GetTypeReader(Type targetType)
        {
            if (targetType.IsArray && targetType.GetArrayRank() > 1)
                targetType = typeof(Array);

            ContentTypeReader reader;
            if (_contentReaders.TryGetValue(targetType, out reader))
                return reader;

            return null;
        }

        internal ContentTypeReader[] LoadAssetReaders(ContentReader reader)
        {
            // The first content byte i read tells me the number of content readers in this XNB file
            var numberOfReaders = reader.Read7BitEncodedInt();
            var contentReaders = new ContentTypeReader[numberOfReaders];
            var needsInitialize = new BitArray(numberOfReaders);
            _contentReaders = new Dictionary<Type, ContentTypeReader>(numberOfReaders);

            // Keep reader creation and initialization serialized across concurrent loads.
            lock (_locker)
            {
                // For each reader in the file, we read out the length of the string which contains the type of the reader,
                // then we read out the string and instantiate the reader through its registered factory.
                for (var i = 0; i < numberOfReaders; i++)
                {
                    // This string tells us what reader we need to decode the following data
                    string originalReaderTypeString = reader.ReadString();

                    Func<ContentTypeReader> readerFunc;
                    if (!TryGetCreator(originalReaderTypeString, out readerFunc))
                    {
                        throw new ContentLoadException("No registered XNB content reader: " + originalReaderTypeString
                            + ". Register the reader with ContentTypeReaderManager.AddTypeCreator before loading this asset.");
                    }

                    contentReaders[i] = readerFunc();
                    needsInitialize[i] = true;

                    var targetType = contentReaders[i].TargetType;
                    if (targetType != null)
                        if (!_contentReaders.ContainsKey(targetType))
                            _contentReaders.Add(targetType, contentReaders[i]);

                    // I think the next 4 bytes refer to the "Version" of the type reader,
                    // although it always seems to be zero
                    reader.ReadInt32();
                }

                // Initialize any new readers.
                for (var i = 0; i < contentReaders.Length; i++)
                {
                    if (needsInitialize.Get(i))
                        contentReaders[i].Initialize(this);
                }

            } // lock (_locker)

            return contentReaders;
        }

        /// <summary>
        /// Removes the Version, Culture, and PublicKeyToken from a fully-qualified type name string.
        /// </summary>
        /// <remarks>
        /// Supports multiple generic types (e.g. Dictionary&lt;TKey,TValue&gt;) and nested generic types (e.g. List&lt;List&lt;int&gt;&gt;).
        /// </remarks>
        /// <param name="type">A string containing the fully-qualified type name to prepare.</param>
        /// <returns>A new string with the Version, Culture and PublicKeyToken removed.</returns>
        public static string PrepareType(string type)
        {
            //Needed to support nested types
            int count = type.Split(new[] { "[[" }, StringSplitOptions.None).Length - 1;

            string preparedType = type;

            for (int i = 0; i < count; i++)
            {
                preparedType = Regex.Replace(preparedType, @"\[(.+?), Version=.+?\]", "[$1]");
            }

            //Handle non generic types
            if (preparedType.Contains("PublicKeyToken"))
                preparedType = Regex.Replace(preparedType, @"(.+?), Version=.+?$", "$1");

            preparedType = preparedType.Replace(", Microsoft.Xna.Framework.Graphics", string.Format(", {0}", _assemblyName));
            preparedType = preparedType.Replace(", Microsoft.Xna.Framework.Video", string.Format(", {0}", _assemblyName));
            preparedType = preparedType.Replace(", Microsoft.Xna.Framework", string.Format(", {0}", _assemblyName));

            if (_isRunningOnNetCore)
                preparedType = preparedType.Replace("mscorlib", "System.Private.CoreLib");
            else
                preparedType = preparedType.Replace("System.Private.CoreLib", "mscorlib");

            return preparedType;
        }

        // Match the registered built-ins across XNB assembly versions and the XNA/MonoGame core-library aliases.
        private static string CanonicalReaderName(string name)
        {
            return Regex.Replace(PrepareType(name), @",\s*(Version|Culture|PublicKeyToken)=[^,\]]+", string.Empty);
        }

        private static void RegisterBuiltIn<T>(Func<T> create) where T : ContentTypeReader
        {
            Func<ContentTypeReader> factory = () => create();
            BuiltInCreators.Add(CanonicalReaderName(typeof(T).FullName), factory);
            BuiltInCreators.Add(CanonicalReaderName(typeof(T).AssemblyQualifiedName), factory);
        }

        private static bool TryGetCreator(string name, out Func<ContentTypeReader> create)
        {
            return typeCreators.TryGetValue(name, out create) || BuiltInCreators.TryGetValue(CanonicalReaderName(name), out create);
        }

        /// <summary>Tests whether the XNB reader name has an explicit factory.</summary>
        /// <param name="typeString">The name stored in the XNB reader table.</param>
        /// <returns>True when a reader factory is available.</returns>
        public static bool HasTypeCreator(string typeString)
        {
            lock (_locker)
            {
                return TryGetCreator(typeString, out _);
            }
        }

        // Custom readers use their exact XNB names and an explicit factory.
        private static Dictionary<string, Func<ContentTypeReader>> typeCreators = new Dictionary<string, Func<ContentTypeReader>>();

        /// <summary>
        /// Registers a function to create a <see cref="ContentTypeReader"/> instance used to read an object of the
        /// type specified.
        /// Call this method to register content readers that may fail loading when trimming or PublishAot are used.
        /// </summary>
        /// <param name='typeString'>A string containing the fully-qualified type name of the object type.</param>
        /// <param name='createFunction'>The function responsible for creating an instance of the <see cref="ContentTypeReader"/> class.</param>
        /// <exception cref="ArgumentNullException">If the <paramref name="typeString"/> parameter is null or an empty string.</exception>
        public static void AddTypeCreator(string typeString, Func<ContentTypeReader> createFunction)
        {
            if (!typeCreators.ContainsKey(typeString))
                typeCreators.Add(typeString, createFunction);
        }

        /// <summary>
        /// Clears custom content type creators that were registered with <see cref="AddTypeCreator(string, Func{ContentTypeReader})"/>
        /// </summary>
        public static void ClearTypeCreators()
        {
            typeCreators.Clear();
        }

    }
}
