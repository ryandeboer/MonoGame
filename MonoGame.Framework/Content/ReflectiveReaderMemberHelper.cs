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


namespace Microsoft.Xna.Framework.Content
{
    using Microsoft.Xna.Framework;
    using System;
    using System.Globalization;
    using System.Reflection;

    internal class ReflectiveReaderMemberHelper
    {
        private bool canWrite;
        private FieldInfo fieldInfo;
        private PropertyInfo propertyInfo;
        private bool sharedResource;
        private ContentTypeReader typeReader;

        private ReflectiveReaderMemberHelper(ContentTypeReaderManager manager, FieldInfo fieldInfo, PropertyInfo propertyInfo, Type memberType, bool canWrite)
        {
            this.typeReader = manager.GetTypeReader(memberType);
            this.fieldInfo = fieldInfo;
            this.propertyInfo = propertyInfo;
            this.canWrite = canWrite;
            if (fieldInfo != null)
            {
                this.sharedResource = IsSharedResource(fieldInfo);
            }
            else
            {
                this.sharedResource = IsSharedResource(propertyInfo);
            }
        }

        private void FixupSharedResource(ContentReader input, object parentInstance, object value)
        {
            if (!this.typeReader.TargetType.IsAssignableFrom(value.GetType()))
            {
                throw new ContentLoadException("Bad Xnb"); // input.CreateContentLoadException(FrameworkResources.BadXnbWrongType, new object[] { value.GetType(), this.typeReader.TargetType });
            }
            if (this.propertyInfo != null)
            {
                this.propertyInfo.SetValue(parentInstance, value, null);
            }
            else
            {
                this.fieldInfo.SetValue(parentInstance, value);
            }
        }

        private static bool IsSharedResource(MemberInfo memberInfo)
        {
            Attribute customAttribute = Attribute.GetCustomAttribute(memberInfo, typeof(ContentSerializerAttribute));
            return ((customAttribute != null) && ((ContentSerializerAttribute)customAttribute).SharedResource);
        }

        public void Read(ContentReader input, object parentInstance)
        {
            Action<object> fixup = null;
            if (this.sharedResource)
            {
                if (!this.canWrite)
                {
                    throw new InvalidOperationException("ReadOnlySharedResource");
                }
                if (fixup == null)
                {
                    fixup = delegate(object value)
                    {
                        this.FixupSharedResource(input, parentInstance, value);
                    };
                }
                input.ReadSharedResource<object>(fixup);
            }
            else if (this.canWrite)
            {
                object obj3 = input.ReadObject<object>(this.typeReader, null);
                if (this.propertyInfo != null)
                {
                    this.propertyInfo.SetValue(parentInstance, obj3, null);
                }
                else
                {
                    this.fieldInfo.SetValue(parentInstance, obj3);
                }
            }
            else
            {
                object theValue;
                if (this.propertyInfo != null)
                {
                    theValue = this.propertyInfo.GetValue(parentInstance, null);
                }
                else
                {
                    theValue = this.fieldInfo.GetValue(parentInstance);
                }
                if (theValue == null)
                {
                    MemberInfo propertyInfo;
                    if (this.propertyInfo != null)
                    {
                        propertyInfo = this.propertyInfo;
                    }
                    else
                    {
                        propertyInfo = this.fieldInfo;
                    }
                    throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, "Can't Serialize Read Only Null for Property {0} on Type {1}.", new object[] { propertyInfo.Name, propertyInfo.DeclaringType }));
                }
                input.ReadObject<object>(this.typeReader, theValue);
            }
        }

        private static bool ShouldSerializeMember(ContentTypeReaderManager manager, Type declaringType, MemberInfo memberInfo, Type memberType, bool isPublic, bool canRead, bool canWrite)
        {
            if (!canRead)
            {
                return false;
            }
            if (memberInfo.IsDefined(typeof(ContentSerializerIgnoreAttribute), false))
            {
                return false;
            }
            if (!isPublic && (Attribute.GetCustomAttribute(memberInfo, typeof(ContentSerializerAttribute)) == null))
            {
                return false;
            }
            if (!canWrite)
            {
                ContentTypeReader typeReader = manager.GetTypeReader(memberType);
                if (typeReader == null || !typeReader.CanDeserializeIntoExistingObject)
                {
                    return false;
                }
            }
            if (declaringType.IsValueType && IsSharedResource(memberInfo))
            {
                return false;
            }
            return true;
        }

        private static bool ShouldSerializeProperty(ContentTypeReaderManager manager, Type declaringType, PropertyInfo propertyInfo)
        {
            if (propertyInfo.GetIndexParameters().Length > 0)
            {
                return false;
            }
            bool isPublic = true;
            foreach (MethodInfo info in propertyInfo.GetAccessors(true))
            {
                if (info.GetBaseDefinition() != info)
                {
                    return false;
                }
                if (!info.IsPublic)
                {
                    isPublic = false;
                }
            }
            return ShouldSerializeMember(manager, declaringType, propertyInfo, propertyInfo.PropertyType, isPublic, propertyInfo.CanRead, propertyInfo.CanWrite);
        }

        public static ReflectiveReaderMemberHelper TryCreate(ContentTypeReaderManager manager, Type declaringType, FieldInfo fieldInfo)
        {
            bool canRead = true;
            bool canWrite = !fieldInfo.IsInitOnly && !fieldInfo.IsLiteral;
            if (!ShouldSerializeMember(manager, declaringType, fieldInfo, fieldInfo.FieldType, fieldInfo.IsPublic, canRead, canWrite))
            {
                ValidateSkippedMember(fieldInfo);
                return null;
            }
            return new ReflectiveReaderMemberHelper(manager, fieldInfo, null, fieldInfo.FieldType, canWrite);
        }

        public static ReflectiveReaderMemberHelper TryCreate(ContentTypeReaderManager manager, Type declaringType, PropertyInfo propertyInfo)
        {
            if (!ShouldSerializeProperty(manager, declaringType, propertyInfo))
            {
                ValidateSkippedMember(propertyInfo);
                return null;
            }
            return new ReflectiveReaderMemberHelper(manager, null, propertyInfo, propertyInfo.PropertyType, propertyInfo.CanWrite);
        }

        private static void ValidateSkippedMember(MemberInfo memberInfo)
        {
            if (Attribute.GetCustomAttribute(memberInfo, typeof(ContentSerializerAttribute)) != null)
            {
                throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, "Can't Serialize Member {0} on Type {1}", new object[] { memberInfo.Name, memberInfo.DeclaringType }));
            }
        }
    }
}
