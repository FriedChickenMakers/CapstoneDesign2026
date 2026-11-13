#!/usr/bin/env python3
"""Run pure Kotlin contracts with either Unity's compiler jars or a Kotlin SDK."""
import argparse
import os
from pathlib import Path
import re
import shutil
import subprocess


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", required=True)
    parser.add_argument("--main", required=True)
    parser.add_argument("--run-arg", action="append", default=[])
    parser.add_argument("sources", nargs="+")
    args = parser.parse_args()
    lib = Path(os.environ.get("KOTLIN_GRADLE_LIB", "/opt/unity/editors/6000.3.24f1/Editor/Data/PlaybackEngines/AndroidPlayer/Tools/gradle/lib"))
    java = os.environ.get("JAVA_BIN") or shutil.which("java") or "/opt/android-jdk/bin/java"
    jars = sorted(lib.glob("*.jar"))
    stdlib = [p for p in jars if re.fullmatch(r"kotlin-stdlib(?:-\d[^/]*)?\.jar", p.name)]
    coroutines = [p for p in jars if p.name.startswith("kotlinx-coroutines-core")]
    if not stdlib or not any("kotlin-compiler" in p.name for p in jars):
        parser.error("KOTLIN_GRADLE_LIB must contain the Kotlin compiler and standard library jars")
    output = Path(args.output).resolve()
    output.parent.mkdir(parents=True, exist_ok=True)
    runtime = os.pathsep.join(map(str, stdlib + coroutines))
    subprocess.run([java, "-cp", os.pathsep.join(map(str, jars)),
                    "org.jetbrains.kotlin.cli.jvm.K2JVMCompiler", "-no-stdlib", "-no-reflect",
                    "-jvm-target", "17", "-classpath", runtime, "-d", str(output), *args.sources], check=True)
    subprocess.run([java, "-cp", str(output) + os.pathsep + runtime, args.main, *args.run_arg], check=True)


if __name__ == "__main__":
    main()
