//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using System.Collections.Generic;
using System.Linq;
using Gridr.Utils;
using UnityEngine;

namespace ScriptableObjects.Editor
{
    [CreateAssetMenu]
    public class Brushes : ScriptableObject
    {
        [ContextMenu("Load Textures")]
        public void TestLoad()
        {
            ReloadSpritesAndTextures();
        }
        
        public GameObject[] brushes;
        private bool _loadable;
        
        private Sprite[] _sprites;
        private Texture2D[] _textures;
        private Texture2D[] _textures2X;
        private Texture2D[] _textures4X;
        
        public Sprite[] Sprites
        {
            get
            {
                if (_loadable && (_sprites == null || !_sprites.Any()))
                {
                    ReloadSpritesAndTextures();
                }

                return _sprites;
            }
        }

        public bool HasTextures => Sprites != null && Sprites.Length > 0;
        
        public Texture2D[] Textures
        {
            get
            {
                if (NeedsToReloadTextures(_textures)) 
                {
                    ReloadSpritesAndTextures();
                }

                return _textures;
            }
        }
        public Texture2D[] Textures2X
        {
            get
            {
                if (NeedsToReloadTextures(_textures2X))
                {
                    ReloadSpritesAndTextures();
                }

                return _textures2X;
            }
        }
        public Texture2D[] Textures4X
        {
            get
            {
                if (NeedsToReloadTextures(_textures4X))
                {
                    ReloadSpritesAndTextures();
                }

                return _textures4X;
            }
        }

        private bool NeedsToReloadTextures(Texture2D[] texture2Ds)
        {
            return texture2Ds == null 
                   || texture2Ds.Length == 0
                   || texture2Ds.FirstOrDefault(tex => tex == null) 
                   || texture2Ds.Any(c => c == null);
        }

        private void OnValidate()
        {
            ReloadSpritesAndTextures();   
        }
        
        private void ReloadSpritesAndTextures()
        {
            _sprites = LoadSprites();
            _textures = LoadTextures(Sprites);
            _textures2X = LoadTextures(Sprites);
            _textures4X = LoadTextures(Sprites);
            ScaleTextures(_textures2X, 2);
            ScaleTextures(_textures4X, 4);
        }

        
        private Sprite[] LoadSprites()
        {
            List<Sprite> sprites = new List<Sprite>();
            if (brushes != null)
            {
                for (int i = 0; i < brushes.Length; i++)
                {
                    if (brushes[i] != null)
                    {
                        var sr = brushes[i].GetComponent<SpriteRenderer>();
                        if (sr == null)
                        {
                            continue;
                        }
                        var sprite = sr.sprite;
                        sprites.Add(sprite);
                    }
                }
                if (!sprites.Any())
                    _loadable = false;
            }
            
            return sprites.ToArray();
        }
        
        private Texture2D[] LoadTextures(Sprite[] sprites)
        {
            var textures = new List<Texture2D>();
            foreach (var sprite in sprites)
            {
                textures.Add(LoadTexture(sprite));
            }

            return textures.ToArray();
        }
        private Texture2D LoadTexture(Sprite sprite)
        {
            var croppedTexture = new Texture2D( (int)sprite.rect.width, (int)sprite.rect.height );
            var pixels = sprite.texture.GetPixels(  
                (int)sprite.textureRect.x, 
                (int)sprite.textureRect.y, 
                (int)sprite.textureRect.width, 
                (int)sprite.textureRect.height);
            croppedTexture.SetPixels( pixels );
            croppedTexture.Apply();
            return croppedTexture;
        }
        private void ScaleTextures(Texture2D[] textures, int factor)
        {
            foreach (var texture2D in textures)
            {
                TextureScale.Point(texture2D, texture2D.width*factor, texture2D.height*factor);
            }
        }
    }
}
