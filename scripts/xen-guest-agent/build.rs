#[cfg(windows)]
extern crate winres;

#[cfg(windows)]
fn main() {
    let mut res = winres::WindowsResource::new();
    res.set_manifest_file("manifest.xml");
    include!("branding.rs");
    res.compile().unwrap();
}

#[cfg(unix)]
fn main() {}
