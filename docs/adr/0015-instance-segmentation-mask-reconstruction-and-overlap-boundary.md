# ADR 0015: Instance segmentation reconstruction and overlap boundary / 实例分割重建与重叠边界

- Status / 状态: Accepted / 已接受
- Date / 日期: 2026-08-05

## Context / 背景

Instance segmentation exports commonly expose either one direct mask per candidate or a shared prototype tensor plus per-candidate coefficients. Accuracy depends on operations that are often omitted from informal model descriptions: tensor layout, activation, grid-to-input interpolation, pixel-center convention, candidate-box crop space and order, binary threshold order, and whether box NMS happens before expensive mask reconstruction. Guessing any of these changes edge pixels and measured accuracy. / 实例分割导出通常输出逐候选直接掩码，或共享原型张量加逐候选系数。精度取决于非正式模型说明经常省略的操作：张量布局、激活、网格到输入的插值、像素中心约定、候选框裁剪空间与顺序、二值阈值顺序，以及是否在昂贵掩码重建前执行边界框 NMS。猜测其中任何一项都会改变边缘像素和实测精度。

Instance masks may overlap. Destructively assigning a pixel to one instance loses valid per-instance information and cannot represent the output of many official references. At the same time, rendering and downstream tracking sometimes require one deterministic owner. Large candidate/prototype tensors and source-sized masks also require explicit allocation limits and cancellation points. / 实例掩码可能重叠。破坏性地将像素只分给一个实例会丢失有效逐实例信息，也无法表示许多官方参考实现的输出；但渲染和下游跟踪有时需要唯一且确定的所有者。大型候选/原型张量与源图尺寸掩码还需要显式分配限制和取消点。

## Decision / 决策

1. `JYPPX.DeploySharp.Visual` owns backend-neutral Direct and Prototype schemas. Candidate boxes, scores, classes, masks/prototypes, and coefficients are strictly named; undeclared output tensors are rejected. Float32 and Float64 are supported. Classes are finite non-negative integer-valued numbers. / `JYPPX.DeploySharp.Visual` 拥有后端无关的 Direct 与 Prototype Schema。候选框、分数、类别、掩码/原型及系数均严格命名；拒绝未声明输出张量。支持 Float32 与 Float64，类别必须是有限非负整数值。
2. Direct layouts are exactly `[1,N,H,W]` and `[1,N,H,W,1]`. Prototype layouts are exactly `[1,C,H,W]` and `[1,H,W,C]`; coefficients are `[1,N,C]` and reconstruct `sum(coeff[c] * prototype[c,y,x])`. No exporter-specific packed row is inferred. / Direct 布局精确为 `[1,N,H,W]` 与 `[1,N,H,W,1]`。Prototype 布局精确为 `[1,C,H,W]` 与 `[1,H,W,C]`；系数为 `[1,N,C]`，并按 `sum(coeff[c] * prototype[c,y,x])` 重建。不会推断导出器特有的打包行。
3. Value kind, sigmoid or no activation, nearest/half-pixel bilinear/align-corners bilinear, model-space candidate crop, crop order, and threshold order are mandatory explicit schema semantics. Before-resize thresholding is limited to nearest neighbor so no hidden second threshold is needed. / 数值类型、sigmoid 或无激活、最近邻/半像素双线性/align-corners 双线性、模型空间候选框裁剪、裁剪顺序和阈值顺序均是必须显式声明的 Schema 语义。阈值前置仅允许最近邻，因此不需要隐藏的第二次阈值化。
4. Candidate score filtering and the existing class-aware/class-agnostic box IoU NMS run before mask materialization. Equal scores retain the smaller original source index. / 候选分数筛选及现有分类别/忽略类别边界框 IoU NMS 在掩码实体化前执行；同分保留更小的原始源索引。
5. Results always own full source-image row-major byte masks with explicit coordinate space and origin. The default retains independent overlapping masks. `ScorePriorityOwnership` additionally returns a source-sized owner map; it never modifies independent masks and resolves each pixel by score descending then source index ascending. / 结果始终拥有完整源图尺寸的行优先 byte 掩码，并显式声明坐标空间和原点。默认保留相互重叠的独立掩码。`ScorePriorityOwnership` 额外返回源图尺寸所有权图；它绝不修改独立掩码，并按分数降序再按源索引升序解决每个像素。
6. Binary RLE stores ordered foreground start/length runs in row-major order and identifies itself as `deploysharp-row-major-foreground-runs-v1`. It is deliberately not COCO compressed RLE. / 二值 RLE 按行优先顺序存储有序前景 start/length 游程，并标识为 `deploysharp-row-major-foreground-runs-v1`；它有意不是 COCO 压缩 RLE。
7. Candidate count, retained instances, prototype channels, tensor/result mask pixels, result bytes, RLE runs, and workspace bytes are independently bounded. Cancellation is observed across candidate, prototype, source-pixel, RLE-adjacent, ownership, and NMS work. Decoders are stateless and do not share mutable buffers. / 候选数、保留实例、原型通道、张量/结果掩码像素、结果字节、RLE 游程及工作区字节分别设限。在候选、原型、源图像素、RLE 相关、所有权和 NMS 工作中观察取消。解码器无状态且不共享可变缓冲区。

## Consequences / 影响

The generic schemas can represent verified exporter contracts without taking OpenCV, ONNX Runtime, or OpenVINO dependencies. They do not claim YOLO-seg, Mask R-CNN, or another named algorithm: such a model becomes `AlgorithmVerified` only after preprocessing, exact exporter tensor interpretation, mask reconstruction/crop/resize order, NMS, and golden outputs match its official reference. Repository-owned constant fixtures remain `ContractVerified` only. / 通用 Schema 可表示已验证导出器契约且不依赖 OpenCV、ONNX Runtime 或 OpenVINO。它们不声明 YOLO-seg、Mask R-CNN 或其他命名算法；只有预处理、精确导出器张量解释、掩码重建/裁剪/缩放顺序、NMS 及黄金输出均与官方参考实现一致后，模型才成为 `AlgorithmVerified`。仓库自有常量夹具仅为 `ContractVerified`。

Independent source-sized masks may consume more memory than a destructive label map, so conservative limits can reject large candidate/source combinations. Applications needing only one rendering surface may opt into the ownership map but still receive independent masks. Model-specific compressed representations can be added later without changing this canonical ownership contract. / 独立源图尺寸掩码可能比破坏性标签图消耗更多内存，因此保守限制会拒绝大型候选/源图组合。只需单一渲染表面的应用可选择所有权图，但仍会收到独立掩码。后续可添加模型特有压缩表示而无需改变此规范所有权契约。
