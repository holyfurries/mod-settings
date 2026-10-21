use super::*;
use zip::write::SimpleFileOptions;

fn fixture() -> Result<tempfile::TempDir> {
    let directory = tempfile::tempdir()?;
    let root = directory.path();
    for name in ["src", "package", "dist"] {
        fs::create_dir(root.join(name))?;
    }
    let manifest = json!({"name":"TestMod", "version_number":"1.2.3", "description":"Test", "website_url":"https://example.com", "dependencies":["holyfurries-ModSettings-0.3.0"]});
    fs::write(root.join("package/manifest.json"), manifest.to_string())?;
    fs::write(
        root.join("TestMod.csproj"),
        "<Project><PropertyGroup><Version>1.2.3</Version></PropertyGroup></Project>",
    )?;
    fs::write(
        root.join("src/Main.cs"),
        r#"[assembly: MelonInfo(typeof(Test.Main), "Test", "1.2.3", "Test")]"#,
    )?;
    for name in ["README.md", "LICENSE", "package/icon-LICENSE"] {
        fs::write(root.join(name), "Test")?;
    }
    fs::write(root.join("package/TestMod.dll"), b"MZtest")?;
    fs::write(
        root.join("package/icon.png"),
        b"\x89PNG\r\n\x1a\n\0\0\0\rIHDR\0\0\x01\0\0\0\x01\0\x08\x02\0\0\0\0\0\0\0",
    )?;
    Ok(directory)
}

fn archive(root: &Path, manifest: &Value, extra: bool) -> Result<()> {
    let mut archive = zip::ZipWriter::new(fs::File::create(
        root.join("dist/holyfurries-TestMod-1.2.3.zip"),
    )?);
    let options = SimpleFileOptions::default();
    let mut manifest = manifest.clone();
    manifest["namespace"] = json!("holyfurries");
    archive.start_file("manifest.json", options)?;
    archive.write_all(manifest.to_string().as_bytes())?;
    for (name, path) in [
        ("README.md", "README.md"),
        ("LICENSE", "LICENSE"),
        ("icon.png", "package/icon.png"),
        ("icon-LICENSE", "package/icon-LICENSE"),
        ("Mods/TestMod.dll", "package/TestMod.dll"),
    ] {
        archive.start_file(name, options)?;
        archive.write_all(&fs::read(root.join(path))?)?;
    }
    if extra {
        archive.start_file("Assembly-CSharp.dll", options)?;
        archive.write_all(b"private reference")?;
    }
    archive.finish()?;
    Ok(())
}

#[test]
fn release_versions_and_tags_must_agree() -> Result<()> {
    let directory = fixture()?;
    let root = directory.path();
    assert!(validate_metadata(root, Some("v1.2.3")).is_ok());
    assert!(validate_metadata(root, Some("v1.2.4")).is_err());
    fs::write(
        root.join("src/Main.cs"),
        r#"[assembly: MelonInfo(typeof(Test.Main), "Test", "1.2.4", "Test")]"#,
    )?;
    assert!(validate_metadata(root, None).is_err());
    Ok(())
}

#[test]
fn exact_package_contents_and_icon_size_are_required() -> Result<()> {
    let directory = fixture()?;
    let root = directory.path();
    let manifest = validate_metadata(root, None)?;
    archive(root, &manifest, false)?;
    let filename = "holyfurries-TestMod-1.2.3.zip";
    assert!(validate_package(root, &manifest, filename).is_ok());
    archive(root, &manifest, true)?;
    assert!(validate_package(root, &manifest, filename).is_err());
    fs::write(root.join("package/icon.png"), b"incorrect icon")?;
    archive(root, &manifest, false)?;
    assert!(validate_package(root, &manifest, filename).is_err());
    Ok(())
}

#[test]
fn changed_manifest_and_stale_dll_are_rejected() -> Result<()> {
    let directory = fixture()?;
    let root = directory.path();
    let manifest = validate_metadata(root, None)?;
    let mut changed = manifest.clone();
    changed["dependencies"] = json!([]);
    archive(root, &changed, false)?;
    let filename = "holyfurries-TestMod-1.2.3.zip";
    assert!(validate_package(root, &manifest, filename).is_err());
    archive(root, &manifest, false)?;
    fs::write(root.join("package/TestMod.dll"), b"MZnew build")?;
    assert!(validate_package(root, &manifest, filename).is_err());
    Ok(())
}
