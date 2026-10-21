use regex::Regex;
use serde_json::{Value, json};
use std::error::Error;
use std::fs::{self, OpenOptions};
use std::io::{Read, Write};
use std::path::Path;
use std::process::ExitCode;

type Result<T> = std::result::Result<T, Box<dyn Error>>;

fn main() -> ExitCode {
    match run() {
        Ok(()) => ExitCode::SUCCESS,
        Err(error) => {
            eprintln!("mod-release: {error}");
            ExitCode::FAILURE
        }
    }
}

fn run() -> Result<()> {
    let arguments: Vec<String> = std::env::args().skip(1).take(2).collect();
    let mode = match arguments.as_slice() {
        [mode] if mode == "metadata" || mode == "package" => mode,
        _ => return Err("usage: mod-release metadata | package".into()),
    };
    let root = Path::new(env!("CARGO_MANIFEST_DIR"));
    let tag = if std::env::var("GITHUB_REF_TYPE").as_deref() == Ok("tag") {
        Some(std::env::var("GITHUB_REF_NAME")?)
    } else {
        None
    };
    let manifest = validate_metadata(root, tag.as_deref())?;
    let name = manifest["name"].as_str().ok_or("missing name")?;
    let version = manifest["version_number"]
        .as_str()
        .ok_or("missing version")?;
    let filename = format!("holyfurries-{name}-{version}.zip");
    if mode == "package" {
        validate_package(root, &manifest, &filename)?;
    }
    fs::create_dir_all(root.join("dist"))?;
    fs::write(
        root.join("dist/thunderstore.toml"),
        configuration(&manifest)?,
    )?;
    let output = format!("name={name}\nversion={version}\nfilename={filename}\n");
    if let Some(path) = std::env::var_os("GITHUB_OUTPUT") {
        OpenOptions::new()
            .create(true)
            .append(true)
            .open(path)?
            .write_all(output.as_bytes())?;
    }
    print!("{output}");
    Ok(())
}

fn read_file(path: &Path, size_bytes_max: u64) -> Result<Vec<u8>> {
    assert!(size_bytes_max > 0);
    assert!(size_bytes_max <= 16_777_216);
    let mut bytes = Vec::new();
    fs::File::open(path)?
        .take(size_bytes_max + 1)
        .read_to_end(&mut bytes)?;
    if bytes.is_empty() || bytes.len() as u64 > size_bytes_max {
        return Err(format!(
            "file must contain 1 to {size_bytes_max} bytes: {}",
            path.display()
        )
        .into());
    }
    Ok(bytes)
}

fn validate_metadata(root: &Path, tag: Option<&str>) -> Result<Value> {
    let manifest: Value =
        serde_json::from_slice(&read_file(&root.join("package/manifest.json"), 65_536)?)?;
    let name = manifest["name"].as_str().ok_or("missing package name")?;
    if name.is_empty()
        || name.len() > 64
        || !name
            .bytes()
            .all(|byte| byte.is_ascii_alphanumeric() || byte == b'_')
    {
        return Err("invalid package name".into());
    }
    let version = manifest["version_number"]
        .as_str()
        .ok_or("missing version")?;
    if !Regex::new(r"^[0-9]+\.[0-9]+\.[0-9]+$")?.is_match(version) || version.len() > 32 {
        return Err("expected a three-part release version".into());
    }
    let project_bytes = read_file(&root.join(format!("{name}.csproj")), 65_536)?;
    let project = roxmltree::Document::parse(std::str::from_utf8(&project_bytes)?)?;
    let project_version = project
        .descendants()
        .find(|node| node.has_tag_name("Version"))
        .and_then(|node| node.text());
    if project_version != Some(version) {
        return Err("project and manifest versions differ".into());
    }
    let source = read_file(&root.join("src/Main.cs"), 1_048_576)?;
    let source = std::str::from_utf8(&source)?;
    let pattern = Regex::new(r#"MelonInfo\(typeof\([^)]*\),\s*"[^"]*",\s*"([^"]+)""#)?;
    if pattern
        .captures(source)
        .and_then(|capture| capture.get(1))
        .map(|capture| capture.as_str())
        != Some(version)
    {
        return Err("MelonInfo and manifest versions differ".into());
    }
    if tag.is_some_and(|tag| tag != format!("v{version}")) {
        return Err("release tag must match v<manifest version>".into());
    }
    let description = manifest["description"]
        .as_str()
        .ok_or("missing description")?;
    if description.chars().count() > 250 {
        return Err("description exceeds 250 characters".into());
    }
    manifest["website_url"]
        .as_str()
        .ok_or("missing website URL")?;
    let dependencies = manifest["dependencies"]
        .as_array()
        .ok_or("missing dependencies")?;
    if dependencies.len() > 32 {
        return Err("too many dependencies".into());
    }
    let dependency_pattern = Regex::new(r"^[A-Za-z0-9_]+-[A-Za-z0-9_]+-[0-9]+\.[0-9]+\.[0-9]+$")?;
    for dependency in dependencies {
        let dependency = dependency.as_str().ok_or("dependency must be a string")?;
        if dependency.len() > 200 || !dependency_pattern.is_match(dependency) {
            return Err("invalid dependency".into());
        }
    }
    Ok(manifest)
}

fn configuration(manifest: &Value) -> Result<String> {
    let name = manifest["name"].as_str().ok_or("missing name")?;
    let mut output = format!(
        "[config]\nschemaVersion = \"0.0.1\"\n\n[package]\nnamespace = \"holyfurries\"\nname = {}\nversionNumber = {}\ndescription = {}\nwebsiteUrl = {}\ncontainsNsfwContent = false\n\n[package.dependencies]\n",
        manifest["name"],
        manifest["version_number"],
        manifest["description"],
        manifest["website_url"]
    );
    for dependency in manifest["dependencies"]
        .as_array()
        .ok_or("missing dependencies")?
    {
        let (name, version) = dependency
            .as_str()
            .ok_or("invalid dependency")?
            .rsplit_once('-')
            .ok_or("invalid dependency")?;
        output.push_str(&format!("{} = {}\n", json!(name), json!(version)));
    }
    output.push_str(&format!(
        "\n[build]\nicon = \"../package/icon.png\"\nreadme = \"../README.md\"\noutdir = \".\"\n\n[[build.copy]]\nsource = \"../LICENSE\"\ntarget = \"LICENSE\"\n\n[[build.copy]]\nsource = \"../package/icon-LICENSE\"\ntarget = \"icon-LICENSE\"\n\n[[build.copy]]\nsource = \"../package/{name}.dll\"\ntarget = \"Mods/{name}.dll\"\n\n[publish]\nrepository = \"https://thunderstore.io\"\ncommunities = [\"schedule-i\"]\n\n[publish.categories]\nschedule-i = []\n"
    ));
    Ok(output)
}

fn validate_package(root: &Path, manifest: &Value, filename: &str) -> Result<()> {
    let name = manifest["name"].as_str().ok_or("missing name")?;
    let assembly = read_file(&root.join(format!("package/{name}.dll")), 16_777_216)?;
    if !assembly.starts_with(b"MZ") {
        return Err("package does not contain a Windows-format assembly".into());
    }
    let bytes = read_file(&root.join("dist").join(filename), 16_777_216)?;
    let mut archive = zip::ZipArchive::new(std::io::Cursor::new(bytes))?;
    let expected = [
        ("README.md".to_owned(), "README.md".to_owned()),
        ("LICENSE".to_owned(), "LICENSE".to_owned()),
        ("icon.png".to_owned(), "package/icon.png".to_owned()),
        ("icon-LICENSE".to_owned(), "package/icon-LICENSE".to_owned()),
        (format!("Mods/{name}.dll"), format!("package/{name}.dll")),
    ];
    if archive.len() != expected.len() + 1 {
        return Err("unexpected files in package".into());
    }
    for (name, path) in &expected {
        let source = read_file(&root.join(path), 16_777_216)?;
        let mut actual = Vec::new();
        archive
            .by_name(name)?
            .take(16_777_217)
            .read_to_end(&mut actual)?;
        if source != actual {
            return Err(format!("packaged file differs from source: {name}").into());
        }
        if name == "icon.png" {
            validate_icon(&actual)?;
        }
    }
    let mut bytes = Vec::new();
    archive
        .by_name("manifest.json")?
        .take(65_537)
        .read_to_end(&mut bytes)?;
    if bytes.len() > 65_536 {
        return Err("packaged manifest is too large".into());
    }
    let mut actual: Value = serde_json::from_slice(&bytes)?;
    let object = actual
        .as_object_mut()
        .ok_or("packaged manifest must be an object")?;
    if object.remove("namespace") != Some(json!("holyfurries")) {
        return Err("incorrect package namespace".into());
    }
    object.remove("FullName");
    if actual != *manifest {
        return Err("packaged manifest differs from source".into());
    }
    Ok(())
}

fn validate_icon(bytes: &[u8]) -> Result<()> {
    if bytes.len() < 33 || !bytes.starts_with(b"\x89PNG\r\n\x1a\n") || &bytes[12..16] != b"IHDR" {
        return Err("icon must be a PNG".into());
    }
    if bytes[16..24] != [0, 0, 1, 0, 0, 0, 1, 0] {
        return Err("icon must be 256 by 256 pixels".into());
    }
    Ok(())
}

#[cfg(test)]
#[path = "tests.rs"]
mod tests;
