#region License
/*
Microsoft Public License (Ms-PL)
MonoGame - Copyright © 2009 The MonoGame Team

All rights reserved.

This license governs use of the accompanying software. If you use the software, you accept this license. If you do not
accept the license, do not use the software.

1. Definitions
The terms "reproduce," "reproduction," "derivative works," and "distribution" have the same meaning here as under 
U.S. copyright law.

A "contribution" is the original software, or any additions or changes to the software.
A "contributor" is any person that distributes its contribution under this license.
"Licensed patents" are a contributor's patent claims that read directly on its contribution.

2. Grant of Rights
(A) Copyright Grant- Subject to the terms of this license, including the license conditions and limitations in section 3, 
each contributor grants you a non-exclusive, worldwide, royalty-free copyright license to reproduce its contribution, prepare derivative works of its contribution, and distribute its contribution or any derivative works that you create.
(B) Patent Grant- Subject to the terms of this license, including the license conditions and limitations in section 3, 
each contributor grants you a non-exclusive, worldwide, royalty-free license under its licensed patents to make, have made, use, sell, offer for sale, import, and/or otherwise dispose of its contribution in the software or derivative works of the contribution in the software.

3. Conditions and Limitations
(A) No Trademark License- This license does not grant you rights to use any contributors' name, logo, or trademarks.
(B) If you bring a patent claim against any contributor over patents that you claim are infringed by the software, 
your patent license from such contributor to the software ends automatically.
(C) If you distribute any portion of the software, you must retain all copyright, patent, trademark, and attribution 
notices that are present in the software.
(D) If you distribute any portion of the software in source code form, you may do so only under this license by including 
a complete copy of this license with your distribution. If you distribute any portion of the software in compiled or object 
code form, you may only do so under a license that complies with this license.
(E) The software is licensed "as-is." You bear the risk of using it. The contributors give no express warranties, guarantees
or conditions. You may have additional consumer rights under your local laws which this license cannot change. To the extent
permitted under your local laws, the contributors exclude the implied warranties of merchantability, fitness for a particular
purpose and non-infringement.
*/
#endregion License


using System;
using System.Reflection;

using Microsoft.Xna.Framework.Content;

namespace Microsoft.Xna.Framework.Content
{
    using Microsoft.Xna.Framework;
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Reflection;

    internal class ReflectiveReader<T> : ContentTypeReader
    {
        private ContentTypeReader baseReader;
        private ConstructorInfo instanceConstructor;
        private List<ReflectiveReaderMemberHelper> memberHelpers;
        private int typeVersion;

        public ReflectiveReader()
            : base(typeof(T))
        {
            this.memberHelpers = new List<ReflectiveReaderMemberHelper>();
            this.instanceConstructor = typeof(T).GetConstructor(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance, null, new Type[0], null);
            object[] customAttributes = typeof(T).GetCustomAttributes(typeof(ContentSerializerTypeVersionAttribute), false);
            if (customAttributes.Length == 1)
            {
                this.typeVersion = ((ContentSerializerTypeVersionAttribute)customAttributes[0]).TypeVersion;
            }
        }

        protected internal override void Initialize(ContentTypeReaderManager manager)
        {
            Type baseType = base.TargetType.BaseType;
            if (((baseType != null) && (baseType != typeof(object))) && (baseType != typeof(ValueType)))
            {
                this.baseReader = manager.GetTypeReader(baseType);
            }
            BindingFlags bindingAttr = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;
            PropertyInfo[] properties = base.TargetType.GetProperties(bindingAttr);
            FieldInfo[] fields = base.TargetType.GetFields(bindingAttr);
            foreach (PropertyInfo info2 in properties)
            {
                ReflectiveReaderMemberHelper item = ReflectiveReaderMemberHelper.TryCreate(manager, base.TargetType, info2);
                if (item != null)
                {
                    this.memberHelpers.Add(item);
                }
            }
            foreach (FieldInfo info in fields)
            {
                ReflectiveReaderMemberHelper helper = ReflectiveReaderMemberHelper.TryCreate(manager, base.TargetType, info);
                if (helper != null)
                {
                    this.memberHelpers.Add(helper);
                }
            }
        }

        protected internal override object Read(ContentReader input, object existingInstance)
        {
            if (input == null)
            {
                throw new ArgumentNullException("input");
            }
            object newInstance = existingInstance;
			if (newInstance == null)
            {
                if (this.instanceConstructor == null)
                {
                    if (!base.TargetType.IsValueType)
                    {
                        throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, "No Default Constructor for {0}", base.TargetType.FullName));
                    }
					newInstance = Activator.CreateInstance(base.TargetType);
                }
                else
                {
					newInstance = this.instanceConstructor.Invoke(null);
                }
            }
			if ((this.baseReader != null) && (this.baseReader.Read(input, newInstance) != newInstance))
            {
                throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, "Reader Constructed New Instance", this.baseReader.GetType().FullName));
            }
            foreach (ReflectiveReaderMemberHelper helper in this.memberHelpers)
            {
				helper.Read(input, newInstance);
            }
			return newInstance;
        }

        public override bool CanDeserializeIntoExistingObject
        {
            get
            {
                return base.TargetType.IsClass;
            }
        }

        public override int TypeVersion
        {
            get
            {
                return this.typeVersion;
            }
        }
    }
}
