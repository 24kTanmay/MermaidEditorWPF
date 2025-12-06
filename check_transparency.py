from PIL import Image
import os

def check_png_transparency():
    icon_folder = "icons"
    files = os.listdir(icon_folder)
    
    for filename in files:
        if filename.endswith(".png"):
            path = os.path.join(icon_folder, filename)
            try:
                img = Image.open(path)
                img = img.convert('RGBA')
                # Check top-left pixel
                pixel = img.getpixel((0, 0))
                print(f"{filename}: Top-left pixel = {pixel}")
                
                # Check if it looks like a white background
                if pixel[3] == 255 and pixel[0] > 240 and pixel[1] > 240 and pixel[2] > 240:
                    print(f"  -> WARNING: {filename} appears to have a white background, not transparent.")
                elif pixel[3] == 0:
                    print(f"  -> OK: {filename} has a transparent background at (0,0).")
                else:
                    print(f"  -> INFO: {filename} has alpha {pixel[3]} at (0,0).")
                    
            except Exception as e:
                print(f"Error checking {filename}: {e}")

if __name__ == "__main__":
    check_png_transparency()
