[CmdletBinding()]
param (
    [Switch]$Downstream
)

$submodules = "xenbus", "xencons", "xenhid", "xeniface", "xennet", "xenvbd", "xenvif", "xenvkbd"

foreach ($submodule in $submodules) {
    # Get submodule head from parent
    $submodule_head_line = git ls-tree HEAD $submodule
    $submodule_head = ($submodule_head_line.Trim() -split '\s+')[2]

    # Get current branch
    $branch = (git -C $submodule rev-parse --abbrev-ref HEAD)

    # Compare with parent repo's commit
    $commits_since_parent = git -C $submodule log --oneline "$submodule_head..HEAD"

    # Compare with master
    $downstream_commits = $null
    if ($Downstream) {
        # Use git cherry to find commits on the branch that are not on master.
        # Equivalent commits (by patch-id) are marked with '-'
        # Unique commits are marked with '+'
        $cherry_output = git -C $submodule cherry master $branch
        $downstream_commits = foreach ($line in $cherry_output) {
            if (-not($line)) {
                continue
            }
            $sign, $sha = $line.Split(' ', 2)
            $oneline = git -C $submodule log -1 --pretty="format:%h %s" $sha
            "$sign $oneline"
        }
    }

    if (($commits_since_parent) -or ($downstream_commits)) {
        Write-Output "--- Submodule: $submodule ---"
        Write-Output "Current branch: $branch"
        Write-Output ""

        if (!$Downstream -and $commits_since_parent) {
            Write-Output "Uncommitted changes from ${submodule_head}:"
            $commits_since_parent
            Write-Output ""
        }

        if ($Downstream -and $downstream_commits) {
            Write-Output "Downstream changes in ${branch}:"
            $downstream_commits
            Write-Output ""
        }

        Write-Output ""
    }
}
