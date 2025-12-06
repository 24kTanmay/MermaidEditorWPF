from PIL import Image
import os

def create_transparent_ico():
    icon_folder = "icons"
    output_file = "Mermaid.ico"
    
    # List of expected sizes and filenames
    sizes = [16, 32, 48, 64, 128, 256]
    images = []

    print("Loading PNGs...")
    for size in sizes:
        filename = f"icon_{size}.png"
        path = os.path.join(icon_folder, filename)
        
        if os.path.exists(path):
            try:
                img = Image.open(path)
                # Ensure RGBA for transparency
                if img.mode != 'RGBA':
                    print(f"Converting {filename} from {img.mode} to RGBA")
                    img = img.convert('RGBA')
                else:
                    print(f"Loaded {filename} (RGBA)")
                
                images.append(img)
            except Exception as e:
                print(f"Error loading {filename}: {e}")
        else:
            print(f"Warning: {filename} not found")

    if images:
        print(f"Saving {output_file} with {len(images)} images...")
        # Save as ICO. The first image is used as the primary one, but all are included.
        # Usually it's best to put the largest one first or last? 
        # Pillow's save method for ICO takes 'append_images' for the rest.
        # We'll use the largest as the base and append the others, or just pass all to save.
        
        # Sort images by size descending just in case, though Pillow handles it.
        images.sort(key=lambda i: i.size[0], reverse=True)
        
        try:
            images[0].save(
                output_file, 
                format='ICO', 
                sizes=[(img.size[0], img.size[1]) for img in images],
                append_images=images[1:]
            )
            print("Success! Mermaid.ico created.")
        except Exception as e:
            print(f"Error saving ICO: {e}")
    else:
        print("No images found to create ICO.")

if __name__ == "__main__":
    create_transparent_ico()
