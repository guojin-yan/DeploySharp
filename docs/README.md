# DeploySharp Documentation
# DeploySharp 文档

This directory contains all documentation configuration and source files for the DeploySharp project.

本目录包含 DeploySharp 项目的所有文档配置和源文件。

## Directory Structure / 目录结构

```
docs/
├── .github/
│   └── workflows/
│       └── docs.yml          # GitHub Actions workflow for doc generation
├── api/
│   └── index.md              # API reference homepage
├── apidoc/                   # Override API documentation
├── articles/                 # Tutorials and guides
│   ├── toc.yml               # Articles table of contents
│   ├── getting-started.md    # Getting started guide
│   ├── installation.md       # Installation instructions
│   ├── object-detection.md   # Object detection tutorial
│   ├── image-segmentation.md # Image segmentation tutorial
│   ├── pose-estimation.md    # Pose estimation tutorial
│   ├── ocr.md                # OCR tutorial
│   └── best-practices.md     # Best practices guide
├── images/                   # Documentation images
├── docfx.json                # DocFX configuration
├── filterConfig.yml          # API filtering rules
├── index.md                  # Documentation homepage
├── toc.yml                   # Main table of contents
└── README.md                 # This file
```

## Generating Documentation Locally / 本地生成文档

### Prerequisites / 先决条件

- .NET SDK 6.0 or later
- DocFX tool

```bash
# Install DocFX
dotnet tool install -g docfx

# Or update if already installed
dotnet tool update -g docfx
```

### Build Steps / 构建步骤

```bash
# 1. Navigate to docs directory
cd docs

# 2. Generate API metadata
docfx metadata

# 3. Build documentation
docfx build

# 4. Serve locally (optional)
docfx serve _site
```

The generated documentation will be in `docs/_site/` directory.

生成的文档将在 `docs/_site/` 目录中。

## Configuration Files / 配置文件

### docfx.json

Main configuration file for DocFX. Defines:
- Source projects for API generation
- Documentation structure
- Template settings
- Output configuration

DocFX 的主配置文件。定义：
- API 生成的源项目
- 文档结构
- 模板设置
- 输出配置

### filterConfig.yml

API filtering rules to exclude:
- System.Object methods
- Compiler-generated code
- Internal/private members
- Test code

API 过滤规则，用于排除：
- System.Object 方法
- 编译器生成的代码
- 内部/私有成员
- 测试代码

## GitHub Actions Workflow / GitHub Actions 工作流

The `.github/workflows/docs.yml` file defines an automated workflow that:

文件 `.github/workflows/docs.yml` 定义了一个自动化工作流：

1. Triggers on push to main/master branch
   在推送到 main/master 分支时触发

2. Builds the solution to generate XML documentation
   构建解决方案以生成 XML 文档

3. Runs DocFX to generate API documentation
   运行 DocFX 生成 API 文档

4. Deploys to GitHub Pages
   部署到 GitHub Pages

### Manual Trigger / 手动触发

You can manually trigger the workflow from GitHub Actions tab.

您可以从 GitHub Actions 选项卡手动触发工作流。

## Writing Documentation / 编写文档

### API Documentation / API 文档

API documentation is automatically generated from XML comments in source code.

API 文档从源代码中的 XML 注释自动生成。

### Articles / 文章

Add new articles to `articles/` directory and update `articles/toc.yml`.

将新文章添加到 `articles/` 目录并更新 `articles/toc.yml`。

### Markdown Syntax / Markdown 语法

- Use standard Markdown for formatting
- Use code blocks with language specifier for examples
- Add both English and Chinese content for bilingual support

- 使用标准 Markdown 进行格式化
- 使用带语言标识符的代码块展示示例
- 添加英文和中文内容以支持双语

## Deployment / 部署

Documentation is automatically deployed to GitHub Pages when:
- Code is pushed to main/master branch
- Documentation files are modified
- Manually triggered via Actions tab

文档在以下情况自动部署到 GitHub Pages：
- 代码推送到 main/master 分支
- 文档文件被修改
- 通过 Actions 选项卡手动触发

### GitHub Pages URL / GitHub Pages 地址

```
https://guojin-yan.github.io/DeploySharp
```

## Contributing / 贡献

When adding new features:
1. Add XML documentation to public APIs
2. Update relevant articles if needed
3. Test documentation generation locally
4. Submit PR with documentation changes

添加新功能时：
1. 为公共 API 添加 XML 文档
2. 如有需要更新相关文章
3. 在本地测试文档生成
4. 提交包含文档更改的 PR

## Troubleshooting / 故障排除

### Build Errors / 构建错误

| Error | Solution |
|-------|----------|
| Missing XML files | Build solution in Release mode first |
| DocFX not found | Install with `dotnet tool install -g docfx` |
| Template errors | Update DocFX to latest version |

### Deployment Issues / 部署问题

| Issue | Solution |
|-------|----------|
| Pages not updating | Check Actions tab for build errors |
| 404 errors | Ensure `.nojekyll` file exists |
| Missing styles | Verify `_site` directory contents |

## Resources / 资源

- [DocFX Documentation](https://dotnet.github.io/docfx/)
- [GitHub Pages Documentation](https://docs.github.com/pages)
- [DeploySharp GitHub Repository](https://github.com/guojin-yan/DeploySharp)
