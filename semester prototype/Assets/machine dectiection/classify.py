import os
import sys
import numpy as np
import tensorflow as tf
import tf_keras

from PIL import Image, ImageOps


# Get the folder containing this Python script
BASE_DIR = os.path.dirname(os.path.abspath(__file__))

MODEL_PATH = os.path.join(BASE_DIR, "keras_model.h5")
LABELS_PATH = os.path.join(BASE_DIR, "labels.txt")

# Custom layer compatibility
class CustomDepthwiseConv2D(tf_keras.layers.DepthwiseConv2D):

    def __init__(self, *args, **kwargs):
        kwargs.pop("groups", None)
        super().__init__(*args, **kwargs)


# Load model
model = tf_keras.models.load_model(
    MODEL_PATH,
    compile=False,
    custom_objects={
        "DepthwiseConv2D": CustomDepthwiseConv2D
    }
)

# Load labels
with open(LABELS_PATH, "r") as file:
    class_names = [
        line.strip().split(" ", 1)[1]
        for line in file.readlines()
    ]


def classify(image_path):

    image = Image.open(image_path).convert("RGB")

    image = ImageOps.fit(
        image,
        (224, 224),
        Image.Resampling.LANCZOS
    )

    image_array = np.asarray(image)

    normalized = (
        image_array.astype(np.float32) / 127.5
    ) - 1

    data = np.expand_dims(normalized, axis=0)

    prediction = model.predict(data, verbose=0)

    index = int(np.argmax(prediction[0]))

    name = class_names[index]

    confidence = float(prediction[0][index])

    # Output machine-readable result
    print(f"{name}|{confidence:.6f}")


if __name__ == "__main__":

    if len(sys.argv) < 2:
        print("ERROR|No image provided")
        sys.exit(1)

    try:
        classify(sys.argv[1])

    except Exception as e:
        print(f"ERROR|{str(e)}")
        sys.exit(1)
