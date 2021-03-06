namespace Felizia

open System

type ContentResponse = {
    Content: string
    Url: string list
}
type Msg =
    | UrlChanged of Url
    | PageNavigation of Url
    | LoadContent of Url
    | ContentLoaded of ContentResponse
    | SetLanguage of string
    | Custom of string

/// The main context
type Model = {
    Sites: Site list
    CurrentSite: Site
    CurrentPage: Page
    PageNumber: int
    CurrentUrl: Url
    Loading: bool
    Language: string
    Version: string

    /// Extra custom info to be used by themes etc
    Extra: Map<string, string>
} with
    member this.T (key: string) =
        let res = this.CurrentSite.I18n.TryFind key
        match res with
        | Some trans -> trans.Other
        | None -> key

    member this.T(key: string, param: obj) =
        let res = this.CurrentSite.I18n.TryFind key
        match res with
        | Some trans -> sprintf (Printf.StringFormat<string->string>(trans.Other)) (param.ToString())
        | None -> key

    static member Empty =
        {
            Loading = false

            CurrentSite = Site.Empty
            Sites = []
            CurrentPage = Page.Empty ()
            PageNumber = 1

            /// The current URL without baseUrl or language
            CurrentUrl = []
            Language = "en"
            Version = "1.0.0"

            Extra = Map.empty
        }

    ///  Creates an absolute URL based on the configured baseURL.
    member x.AbsUrl (url: Url) =
        List.append [ x.CurrentSite.BaseUrl ] url

    /// Adds the absolute URL with correct language prefix according to site configuration for multilingual.
    member x.AbsLangUrl (url: Url) =
        List.append [ x.Language ] url

    /// Adds the relative URL with correct language prefix according to site configuration for multilingual.
    member x.RelLangUrl (url: Url) =
        ()

    /// Creates a baseURL-relative URL.
    member x.RelUrl (url: Url) =
        ()

    /// Gets a Page of a given url.
    static member GetPage (url: Url) (page: Page) =
        match page.Pages, page.Url with
        | _, pageUrl when pageUrl = url -> Some page
        | xs, _ -> List.tryPick (Model.GetPage url) xs
        | _ -> None

    // FIXME: this function does too much.
    member this.SetLanguage (lang: string) : Model =
        let site = List.tryFind (fun (site: Site) -> site.Language.Lang = lang) this.Sites
        match lang, site with
        | lang, Some site when lang <> this.Language ->
            let url =
                let segments = this.CurrentUrl

                // Remove existing language from URL if any.
                if (not << List.isEmpty) segments then
                    //printfn "%A" this.CurrentSite
                    if segments.[0] = this.CurrentSite.Language.Lang then
                        segments |> List.skip 1
                    else
                        segments
                elif lang <> site.DefaultContentLanguage then List.append [ lang ] (List.ofSeq segments)
                else segments

            let result = Model.GetPage url site.Home
            match result with
            | Some page ->
                let page' = { page with Paginator = Some (Paginator(page.Pages, site.Paginate, site.PaginatePath, this.PageNumber, this.CurrentUrl)) }
                { this with CurrentSite = site; CurrentPage=page'; Language = lang; CurrentUrl = url }
            | None ->
                printfn "Did not find page %A with language: %s" url lang
                this
        | lang, Some site when lang = this.Language ->
            let page = this.CurrentPage
            { this with CurrentSite = site; Language = lang; CurrentPage = { page with Paginator = Some (Paginator(page.Pages, site.Paginate, site.PaginatePath, this.PageNumber, this.CurrentUrl)) }}
        | _ ->
            printfn "Did not find site with language: %s" lang
            this

type Dispatch = Msg -> unit

[<AutoOpen>]
module MenuExtensions =
    type Menu
    with
        member this.IsMenuCurrent model =
            this.Url = model.CurrentUrl
