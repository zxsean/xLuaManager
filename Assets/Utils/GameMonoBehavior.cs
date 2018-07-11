using UnityEngine;

namespace GameUtilities
{
    /// <summary>
    /// Base Customer MonoBehavior for simple cache data
    /// </summary>
    //
    public class GameMonoBehaviour : MonoBehaviour
    {
        #region Cached Members

        /// <summary>
        /// auto cache gameObject
        /// </summary>
        private GameObject m_cachedGameObject = null;

        /// <summary>
        /// auto cache transform
        /// </summary>
        private Transform m_cachedTransform = null;

        /// <summary>
        /// auto cache gameObject -- lazy init
        /// </summary>
        public GameObject CachedGameObject
        {
            get
            {
                if (m_cachedGameObject == null)
                {
                    m_cachedGameObject = gameObject;
                }

                return m_cachedGameObject;
            }
        }

        /// <summary>
        /// auto cache transform -- lazy init
        /// </summary>
        public Transform CachedTransform
        {
            get
            {
                if (m_cachedTransform == null)
                {
                    m_cachedTransform = transform;
                }

                return m_cachedTransform;
            }
        }

        /// <summary>
        /// pos access
        /// </summary>
        public Vector3 position
        {
            get
            {
                return CachedTransform.position;
            }
            set
            {
                CachedTransform.position = value;
            }
        }

        public Quaternion rotation
        {
            get
            {
                return CachedTransform.rotation;
            }
            set
            {
                CachedTransform.rotation = value;
            }
        }

        public int InstanceID { get; private set; }                     // auto cache instance ID

        #endregion Cached Members

        #region Mono Funcs

        /// <summary>
        /// recommand call base.Awake first in inherit class
        /// </summary>
        protected virtual void Awake()
        {
            InstanceID = CachedGameObject.GetInstanceID();      // maybe it is dif in runtime
        }

        #endregion Mono Funcs

        #region Factory

        /// <summary>
        /// wrapped func to create a new GameObject whitch component attatched
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="_objectName"></param>
        /// <param name="_setPos"></param>
        /// <returns></returns>
        public static T New<T>(string _objectName = null, Vector3? _setPos = null) where T : GameMonoBehaviour
        {
            var _go = new GameObject(_objectName ?? ("NewObject_" + typeof(T).Name));
            var _comp = _go.AddComponent<T>();

            if (_setPos.HasValue)
            {
                _comp.CachedTransform.position = _setPos.Value;
            }

            return _comp;
        }

        #endregion Factory
    }
}