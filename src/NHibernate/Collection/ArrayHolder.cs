using System;
using System.Collections;
using System.Data;
using log4net;
using NHibernate.Engine;
using NHibernate.Type;

namespace NHibernate.Collection
{
	/// <summary>
	/// A persistent wrapper for an array. lazy initialization is NOT supported
	/// </summary>
	[Serializable]
	public class ArrayHolder : PersistentCollection
	{
		private static readonly ILog log = LogManager.GetLogger( typeof( PersistentCollection ) );

		private object array;

		[NonSerialized]
		private System.Type elementClass;

		[NonSerialized]
		private IList tempList;

		[NonSerialized]
		private IList tempListIdentifier;

		/// <summary>
		/// 
		/// </summary>
		/// <param name="session"></param>
		/// <param name="array"></param>
		public ArrayHolder( ISessionImplementor session, object array ) : base( session )
		{
			this.array = array;
			initialized = true;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="persister"></param>
		/// <returns></returns>
		protected override object Snapshot( CollectionPersister persister )
		{
			int length = /*(array==null) ? temp.Count :*/ ( ( Array ) array ).Length;
			object result = System.Array.CreateInstance( persister.ElementClass, length );
			for( int i = 0; i < length; i++ )
			{
				object elt = /*(array==null) ? temp[i] :*/ ( ( Array ) array ).GetValue( i );
				try
				{
					( ( Array ) result ).SetValue( persister.ElementType.DeepCopy( elt ), i );
				}
				catch( Exception e )
				{
					log.Error( "Array element type error", e );
					throw new HibernateException( "Array element type error", e );
				}
			}
			return result;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="snapshot"></param>
		/// <returns></returns>
		public override ICollection GetOrphans( object snapshot )
		{
			object[ ] sn = ( object[ ] ) snapshot;
			object[ ] arr = ( object[ ] ) array;
			ArrayList result = new ArrayList( sn.Length );
			for( int i = 0; i < sn.Length; i++ )
			{
				result.Add( sn[ i ] );
			}
			for( int i = 0; i < sn.Length; i++ )
			{
				PersistentCollection.IdentityRemove( result, arr[ i ], session );
			}
			return result;
		}


		/// <summary>
		/// 
		/// </summary>
		/// <param name="session"></param>
		/// <param name="persister"></param>
		public ArrayHolder( ISessionImplementor session, CollectionPersister persister )
			: base( session )
		{
			elementClass = persister.ElementClass;
		}

		/// <summary>
		/// 
		/// </summary>
		public object Array
		{
			get { return array; }
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="elementType"></param>
		/// <returns></returns>
		public override bool EqualsSnapshot( IType elementType )
		{
			object snapshot = GetSnapshot();
			int xlen = ( ( Array ) snapshot ).Length;
			if( xlen != ( ( Array ) array ).Length )
			{
				return false;
			}
			for( int i = 0; i < xlen; i++ )
			{
				if( elementType.IsDirty( ( ( Array ) snapshot ).GetValue( i ), ( ( Array ) array ).GetValue( i ), session ) )
				{
					return false;
				}
			}
			return true;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <returns></returns>
		public override ICollection Elements()
		{
			//if (array==null) return tempList;
			int length = ( ( Array ) array ).Length;
			IList list = new ArrayList( length );
			for( int i = 0; i < length; i++ )
			{
				list.Add( ( ( Array ) array ).GetValue( i ) );
			}
			return list;
		}

		/// <summary>
		/// 
		/// </summary>
		public override bool Empty
		{
			get { return false; }
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="st"></param>
		/// <param name="persister"></param>
		/// <param name="entry"></param>
		/// <param name="i"></param>
		/// <param name="writeOrder"></param>
		public override void WriteTo( IDbCommand st, CollectionPersister persister, object entry, int i, bool writeOrder )
		{
			persister.WriteElement( st, entry, writeOrder, session );
			persister.WriteIndex( st, i, writeOrder, session );
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="rs"></param>
		/// <param name="persister"></param>
		/// <param name="owner"></param>
		/// <returns></returns>
		public override object ReadFrom( IDataReader rs, CollectionPersister persister, object owner )
		{
			//object element = persister.ReadElement(rs, owner, session);
			object elementIdentifier = persister.ReadElementIdentifier( rs, owner, session );
			int index = ( int ) persister.ReadIndex( rs, session );
			for( int i = tempList.Count; i <= index; i++ )
			{
				tempList.Insert( i, null );
				tempListIdentifier.Insert( i, null );
			}
			//tempList[index] = element;
			tempListIdentifier[ index ] = elementIdentifier;
			return elementIdentifier;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <returns></returns>
		public override ICollection Entries()
		{
			return Elements();
		}

		/// <summary>
		/// 
		/// </summary>
		public override void BeginRead()
		{
			tempList = new ArrayList();
			tempListIdentifier = new ArrayList();
		}

		/// <summary>
		/// 
		/// </summary>
		[Obsolete( "See PersistentCollection.EndRead for reason." )]
		public override void EndRead()
		{
			array = ( ( ArrayList ) tempList ).ToArray( elementClass );
			tempList = null;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="persister"></param>
		/// <param name="owner"></param>
		public override void EndRead( CollectionPersister persister, object owner )
		{
			array = System.Array.CreateInstance( elementClass, tempListIdentifier.Count );

			for( int i = 0; i < tempListIdentifier.Count; i++ )
			{
				object element = persister.ElementType.ResolveIdentifier( tempListIdentifier[ i ], session, owner );
				( ( Array ) array ).SetValue( element, i );
				tempList[ i ] = element;
			}

			//tempList = null;
			//tempListIdentifier = null;
		}


		/// <summary>
		/// 
		/// </summary>
		/// <param name="lazy"></param>
		/// <returns></returns>
		public override object GetInitialValue( bool lazy )
		{
			base.GetInitialValue( false );
			session.AddArrayHolder( this );
			return array;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="persister"></param>
		public override void BeforeInitialize( CollectionPersister persister )
		{
		}

		/// <summary>
		/// 
		/// </summary>
		public override bool IsArrayHolder
		{
			get { return true; }
		}

		/// <summary>
		/// 
		/// </summary>
		public override bool IsDirectlyAccessible
		{
			get { return true; }
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="session"></param>
		/// <param name="persister"></param>
		/// <param name="disassembled"></param>
		/// <param name="owner"></param>
		public ArrayHolder( ISessionImplementor session, CollectionPersister persister, object disassembled, object owner )
			: base( session )
		{
			object[ ] cached = ( object[ ] ) disassembled;

			array = System.Array.CreateInstance( persister.ElementClass, cached.Length );

			for( int i = 0; i < cached.Length; i++ )
			{
				( ( Array ) array ).SetValue( persister.ElementType.Assemble( cached[ i ], session, owner ), i );
			}
			initialized = true;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="persister"></param>
		/// <returns></returns>
		public override object Disassemble( CollectionPersister persister )
		{
			int length = ( ( Array ) array ).Length;
			object[ ] result = new object[length];
			for( int i = 0; i < length; i++ )
			{
				result[ i ] = persister.ElementType.Disassemble( ( ( Array ) array ).GetValue( i ), session );
			}
			return result;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <returns></returns>
		public override object GetCachedValue()
		{
			session.AddArrayHolder( this );
			return array;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="elemType"></param>
		/// <returns></returns>
		public override ICollection GetDeletes( IType elemType )
		{
			IList deletes = new ArrayList();
			object sn = GetSnapshot();
			int snSize = ( ( Array ) sn ).Length;
			int arraySize = ( ( Array ) array ).Length;
			int end;
			if( snSize > arraySize )
			{
				for( int i = arraySize; i < snSize; i++ )
				{
					deletes.Add( i );
				}
				end = arraySize;
			}
			else
			{
				end = snSize;
			}
			for( int i = 0; i < end; i++ )
			{
				if( ( ( Array ) array ).GetValue( i ) == null && ( ( Array ) sn ).GetValue( i ) != null )
				{
					deletes.Add( i );
				}
			}
			return deletes;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="entry"></param>
		/// <param name="i"></param>
		/// <param name="elemType"></param>
		/// <returns></returns>
		public override bool NeedsInserting( object entry, int i, IType elemType )
		{
			object sn = GetSnapshot();
			return ( ( Array ) array ).GetValue( i ) != null && ( i >= ( ( Array ) sn ).Length || ( ( Array ) sn ).GetValue( i ) == null );
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="entry"></param>
		/// <param name="i"></param>
		/// <param name="elemType"></param>
		/// <returns></returns>
		public override bool NeedsUpdating( object entry, int i, IType elemType )
		{
			object sn = GetSnapshot();
			return i < ( ( Array ) sn ).Length &&
				( ( Array ) sn ).GetValue( i ) != null &&
				( ( Array ) array ).GetValue( i ) != null &&
				elemType.IsDirty( ( ( Array ) array ).GetValue( i ), ( ( Array ) sn ).GetValue( i ), session );
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="entry"></param>
		/// <param name="i"></param>
		/// <returns></returns>
		public override object GetIndex( object entry, int i )
		{
			return i;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="entry"></param>
		/// <param name="i"></param>
		/// <returns></returns>
		public override bool EntryExists( object entry, int i )
		{
			return entry != null;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="array"></param>
		/// <param name="index"></param>
		public override void CopyTo( Array array, int index )
		{
			( ( Array ) this.array ).CopyTo( array, index );
		}

		/// <summary>
		/// 
		/// </summary>
		public override int Count
		{
			get { return ( ( Array ) array ).Length; }
		}

		/// <summary>
		/// 
		/// </summary>
		/// <returns></returns>
		public override IEnumerator GetEnumerator()
		{
			return ( ( Array ) array ).GetEnumerator();
		}

		/// <summary>
		/// 
		/// </summary>
		public override bool IsSynchronized
		{
			get { return false; }
		}

		/// <summary>
		/// 
		/// </summary>
		public override object SyncRoot
		{
			get { return this; }
		}


	}
}