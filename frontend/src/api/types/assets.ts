/** A US-listed stock or fund. */
export interface Security {
  id: string
  symbol: string
  name: string
  /** One line on what the company does, or what the fund tracks. */
  what: string
}

/** One result of the stock and fund search: backend Assets/AssetEndpoints.cs (AssetMatch). */
export interface AssetMatch {
  /** Null until it is added: only the price source knows it yet. */
  id: string | null
  symbol: string
  name: string
  /** The exchange's MIC ("XNYS"). */
  exchange: string
  what: string
}

/** What adding a stock or fund gave: backend Assets/AssetEndpoints.cs (AddedAssetDto). */
export interface AddedAsset {
  asset: Security
  /** False while its price history isn't in: the strategy waits for it. */
  pricesIn: boolean
}

export type AssetKind = 'stock' | 'fund' | 'index'
