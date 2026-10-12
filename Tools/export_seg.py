# Выгрузка модели сегментации для тепловизора GhostWard.
# LRASPP MobileNetV3-Large (torchvision, BSD-3), классы VOC: 15 — человек; 3 птица, 8 кошка, 10 корова, 12 собака, 13 лошадь, 17 овца.
# Вход: 1×3×224×224, RGB 0..1 (нормализация внутри). Выход: 1×2×224×224 — вероятность «человек» и «животное».
import sys
import torch
import torchvision

out_path = sys.argv[1]
SIZE = 224

base = torchvision.models.segmentation.lraspp_mobilenet_v3_large(
    weights=torchvision.models.segmentation.LRASPP_MobileNet_V3_Large_Weights.COCO_WITH_VOC_LABELS_V1)
base.eval()


class Seg(torch.nn.Module):
    def __init__(self, m):
        super().__init__()
        self.m = m
        self.register_buffer('mean', torch.tensor([0.485, 0.456, 0.406]).view(1, 3, 1, 1))
        self.register_buffer('std', torch.tensor([0.229, 0.224, 0.225]).view(1, 3, 1, 1))

    def forward(self, x):
        logits = self.m((x - self.mean) / self.std)['out']
        p = torch.softmax(logits, dim=1)
        person = p[:, 15:16]
        animal = p[:, 3:4] + p[:, 8:9] + p[:, 10:11] + p[:, 12:13] + p[:, 13:14] + p[:, 17:18]
        return torch.cat([person, animal], dim=1)


model = Seg(base).eval()
dummy = torch.rand(1, 3, SIZE, SIZE)
with torch.no_grad():
    y = model(dummy)
print('output', tuple(y.shape))
torch.onnx.export(model, dummy, out_path, input_names=['image'], output_names=['mask'],
                  opset_version=15, dynamo=False, do_constant_folding=True)
print('saved', out_path)
